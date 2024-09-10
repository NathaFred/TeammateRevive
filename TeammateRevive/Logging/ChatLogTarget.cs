using BepInEx.Logging;
using R2API.Utils;
using UnityEngine;

namespace TeammateRevive.Logging
{
    public class ChatLogTarget : ILogTarget
    {
        public void Write(LogLevel level, object msg)
        {
            if (level == LogLevel.Debug || !RunTracker.instance.IsStarted) return;
            
            var color = level switch
            {
                LogLevel.Info => Color.blue,
                LogLevel.Warning => Color.yellow,
                LogLevel.Error => Color.red,
                LogLevel.Fatal => Color.red,
                _ => Color.blue
            };

            //TODO:FIX
            //ChatMessage.SendColored(msg.ToString(), color);
        }
    }
}