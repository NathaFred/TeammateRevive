﻿using System.Collections;
using System.Collections.Generic;
using R2API.Networking.Interfaces;
using RoR2;
using TeammateRevive.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace TeammateRevive.DeathTotem
{
    public class SyncDeathTotemMessage : INetMessage
    {
        public NetworkInstanceId totemId;
        public NetworkInstanceId deadPlayerId;
        public int insideCount;
        public readonly List<NetworkInstanceId> insideIDs = new();
        public float radius;
        private float fractionPerSecond;

        public SyncDeathTotemMessage() 
        {

        }

        public SyncDeathTotemMessage(NetworkInstanceId totemId, NetworkInstanceId deadPlayerId, List<NetworkInstanceId> insideIDs, float radius, float fractionPerSecond)
        {
            this.totemId = totemId;
            this.deadPlayerId = deadPlayerId;
            insideCount = insideIDs.Count;
            this.insideIDs = insideIDs;
            this.radius = radius;
            this.fractionPerSecond = fractionPerSecond;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(totemId);
            writer.Write(deadPlayerId);
            writer.Write(insideCount);
            for (int i = 0; i < insideCount; i++)
            {
                writer.Write(insideIDs[i]);
            }
            writer.Write(radius);
            writer.Write(fractionPerSecond);
        }

        public void Deserialize(NetworkReader reader)
        {
            totemId = reader.ReadNetworkId();
            deadPlayerId = reader.ReadNetworkId();
            insideCount = reader.ReadInt32();
            insideIDs.Clear();
            for (int i = 0; i < insideCount; i++)
            {
                insideIDs.Add(reader.ReadNetworkId());
            }
            radius = reader.ReadSingle();
            fractionPerSecond = reader.ReadSingle();
        }

        public void OnReceived()
        {
            if (NetworkServer.active) return;
            DeathTotemBehavior totemComp = Util.FindNetworkObject(totemId)?.GetComponent<DeathTotemBehavior>();
            if (totemComp == null)
            {
                Log.Debug("Couldn't find totem " + totemId);
                MainTeammateRevival.instance.DoCoroutine(DelayedApply(totemComp));
                return;
            }

            Apply(totemComp);
        }

        private IEnumerator DelayedApply(DeathTotemBehavior totemComp)
        {
            yield return new WaitForSeconds(.3f);
            Apply(totemComp);
        }

        private void Apply(DeathTotemBehavior totemComp)
        {
            totemComp = totemComp ? totemComp : Util.FindNetworkObject(totemId)?.GetComponent<DeathTotemBehavior>();
            if (totemComp == null)
            {
                Log.Debug("Couldn't find totem after delay " + totemId);
                return;
            }
        
            totemComp.gameObject.SetActive(true);
            Log.DebugMethod($"Fraction: {fractionPerSecond}");
            if (totemComp) totemComp.SetValuesReceive(deadPlayerId, insideIDs, radius, fractionPerSecond);
        }
    }
}