﻿using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ProtocolDumper.Infrastructure;

namespace ProtocolDumper;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class ProtocolDumperPlugin : BasePlugin
{
	public override void Load()
	{
		Log.LogInfo($"Plugin '{MyPluginInfo.PLUGIN_NAME}' has successfully been loaded!");
		AddComponent<DumpProtocolBehavior>();
	}

	class DumpProtocolBehavior : MonoBehaviour
	{
		readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(nameof(DumpProtocolBehavior));

		void Start()
		{
			try
			{
				var connectionMessages = Com.Ankama.Dofus.Server.Connection.Protocol.MessageReflection.Descriptor;
			
				logger.LogInfo("Connection messages:");
				logger.LogInfo(connectionMessages.ToProtoFile());
			}
			finally { Destroy(this); } // we only need to run this once
		}

		void OnDestroy()
		{
			try { logger.LogDebug("DumpProtocolBehavior.OnDestroy()"); }
			finally { logger.Dispose(); } // gotta clean up
		}
	}
}