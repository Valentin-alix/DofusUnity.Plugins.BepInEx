﻿using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using ProtocolDumper.Infrastructure;

namespace ProtocolDumper;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class ProtocolDumperPlugin : BasePlugin
{
	private static readonly string DefaultProtocolDumpPath = Path.Combine(Paths.GameRootPath, "protocol");
	static List<Assembly>? _loadedAssemblies;
	static string? _protocolDumpPath;

	public override void Load()
	{
		Log.LogInfo($"Plugin '{MyPluginInfo.PLUGIN_NAME}' has successfully been loaded!");

		var includeDateConfigEntry = Config.Bind("General", "IncludeDate", false,
			"Whether to include the current date and time in the output directory name.");

		var dumpPathConfigEntry = Config.Bind("General", "OutputDirectory", DefaultProtocolDumpPath,
			"The path where the protocol files will be dumped to.");

		_protocolDumpPath ??= includeDateConfigEntry.Value
			? $"{dumpPathConfigEntry.Value}-{DateTime.Now:yyyyMMddTHHmmss}"
			: dumpPathConfigEntry.Value;

		// First we need to make sure that we have all the protocol assemblies loaded
		var gameAssembliesPath = Path.Combine(Paths.BepInExRootPath, "interop");
		var gameAssemblies = Directory.GetFiles(gameAssembliesPath, "*.dll");

		var protocolAssembliesPaths = gameAssemblies
			.Where(static p => p.Contains("Protocol"))
			.ToArray();

		// Compare the protocol assemblies against the loaded assemblies
		var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (var protocolAssemblyPath in protocolAssembliesPaths)
		{
			var assemblyName = Path.GetFileNameWithoutExtension(protocolAssemblyPath);
			Log.LogDebug($"Checking if assembly '{assemblyName}' is loaded...");

			var assembly = loadedAssemblies.FirstOrDefault(
				assembly => assembly.GetName().Name?.Contains(assemblyName) ?? false);

			Log.LogDebug($"Assembly '{assemblyName}' is {(assembly != null ? "already" : "not yet")} loaded.");

			_loadedAssemblies ??= new(gameAssemblies.Length);
			_loadedAssemblies.Add(assembly ?? Assembly.LoadFrom(protocolAssemblyPath));
		}

		// Now we can dump the protocol files, let's create a new GameObject to do that
		AddComponent<DumpProtocolBehavior>();
	}

	public override bool Unload()
	{
		Log.LogInfo($"Plugin '{MyPluginInfo.PLUGIN_NAME}' has successfully been unloaded!");
		_loadedAssemblies?.Clear();
		_loadedAssemblies = null;
		_protocolDumpPath = null;

		return base.Unload();
	}

	class DumpProtocolBehavior : MonoBehaviour
	{
		readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(nameof(DumpProtocolBehavior));

		void Start()
		{
			if (_loadedAssemblies == null)
			{
				logger.LogFatal("No protocol assemblies could be loaded, aborting...");
				DestroyImmediate(this);
				return;
			}

			try
			{
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var ass in assemblies)
                {
                    if (ass.FullName == null || !ass.FullName.StartsWith("Ankama.Dofus.Protocol"))
                        continue;

                    var types = ass.GetTypes();
                    foreach (var t in types)
                    {
                        if (!t.Name.EndsWith("Reflection"))
                            continue;

                        var descriptorProperty = t.GetProperty("Descriptor");

                        if (descriptorProperty == null)
                            continue;

                        var descriptor = descriptorProperty.GetValue(null);
                        var descriptorType = descriptor.GetType();

                        var protoProperty = descriptorType.GetProperty("Proto");
                        var proto = protoProperty.GetValue(descriptor);

                        var toStringMethod = proto.GetType().GetMethod("ToString");
                        var res = (string)toStringMethod.Invoke(proto, Array.Empty<object>());

                        string fullName = t.FullName;

                        int lastDotIndex = fullName.LastIndexOf('.');
                        if (lastDotIndex >= 0)
                        {
                            fullName = fullName.Substring(0, lastDotIndex);
                        }

                        File.WriteAllText("./output/" + fullName + ".json", res);

                        Console.WriteLine(descriptor);
                    }
                }
            }
			finally { DestroyImmediate(this); } // we only need to run this once
		}

		void DumpProtocolFor(Assembly assembly, DirectoryInfo outputDirectory)
		{
			foreach (var type in assembly.GetTypes().Where(static t => t.Name.EndsWith("Reflection")))
			{
				if (!type.TryGetFileDescriptor(out var descriptor))
					continue;

				logger.LogDebug($"Dumping protocol for '{descriptor.Name}'...");
				var protoFileContent = descriptor.ToProtoFile();

				var filePath = Path.Combine(outputDirectory.FullName, descriptor.Name);
				Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
				File.WriteAllText(filePath, protoFileContent);
			}
		}

		void OnDestroy()
		{
			try { logger.LogDebug("DumpProtocolBehavior.OnDestroy()"); }
			finally { logger.Dispose(); } // gotta clean up
		}
	}
}