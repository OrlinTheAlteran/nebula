﻿using HarmonyLib;
using NebulaModel.Logger;
using NebulaModel.Packets.Factory;
using NebulaModel.Packets.Planet;
using NebulaWorld;
using NebulaWorld.Factory;
using NebulaWorld.Planet;
using NebulaWorld.Player;
using UnityEngine;

namespace NebulaPatcher.Patches.Dynamic
{
    [HarmonyPatch(typeof(PlanetFactory))]
    class PlanetFactory_patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlanetFactory.AddPrebuildData))]
        public static void AddPrebuildData_Postfix(PlanetFactory __instance, PrebuildData prebuild, ref int __result)
        {
            if (!SimulatedWorld.Initialized)
                return;

            // If the host game called the method, we need to compute the PrebuildId ourself
            if (LocalPlayer.Instance.IsMasterClient)
            {
                FactoryManager.Instance.SetPrebuildRequest(__instance.planetId, __result, LocalPlayer.Instance.PlayerId);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlanetFactory.BuildFinally))]
        public static bool BuildFinally_Prefix(PlanetFactory __instance, Player player, int prebuildId)
        {
            if (!SimulatedWorld.Initialized)
                return true;

            if (LocalPlayer.Instance.IsMasterClient)
            {
                if (!FactoryManager.Instance.ContainsPrebuildRequest(__instance.planetId, prebuildId))
                {
                    // This prevents duplicating the entity when multiple players trigger the BuildFinally for the same entity at the same time.
                    // If it occurs in any other circumstances, it means that we have some desynchronization between clients and host prebuilds buffers.
                    //Log.Warn($"BuildFinally was called without having a corresponding PrebuildRequest for the prebuild {prebuildId} on the planet {__instance.planetId}");
                    return false;
                }

                // Remove the prebuild request from the list since we will now convert it to a real building
                FactoryManager.Instance.RemovePrebuildRequest(__instance.planetId, prebuildId);
            }

            if (LocalPlayer.Instance.IsMasterClient || !FactoryManager.Instance.IsIncomingRequest.Value)
            {
                LocalPlayer.Instance.SendPacket(new BuildEntityRequest(__instance.planetId, prebuildId, FactoryManager.Instance.PacketAuthor == FactoryManager.Instance.AUTHOR_NONE ? LocalPlayer.Instance.PlayerId : FactoryManager.Instance.PacketAuthor));
            }

            if (!LocalPlayer.Instance.IsMasterClient && !FactoryManager.Instance.IsIncomingRequest.Value && !DroneManager.IsPendingBuildRequest(-prebuildId))
            {
                DroneManager.AddBuildRequestSent(-prebuildId);
            }

            return LocalPlayer.Instance.IsMasterClient || FactoryManager.Instance.IsIncomingRequest.Value;
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpgradeFinally")]
        public static bool UpgradeFinally_Prefix(PlanetFactory __instance, Player player, int objId, ItemProto replace_item_proto)
        {
            if (!SimulatedWorld.Initialized)
                return true;

            if (LocalPlayer.Instance.IsMasterClient || !FactoryManager.Instance.IsIncomingRequest.Value)
            {
                LocalPlayer.Instance.SendPacket(new UpgradeEntityRequest(__instance.planetId, objId, replace_item_proto.ID, FactoryManager.Instance.PacketAuthor == -1 ? LocalPlayer.Instance.PlayerId : FactoryManager.Instance.PacketAuthor));
            }

            return LocalPlayer.Instance.IsMasterClient || FactoryManager.Instance.IsIncomingRequest.Value;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlanetFactory.GameTick))]
        public static bool InternalUpdate_Prefix()
        {
            StorageManager.IsHumanInput = false;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlanetFactory.GameTick))]
        public static void InternalUpdate_Postfix()
        {
            StorageManager.IsHumanInput = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlanetFactory.PasteBuildingSetting))]
        public static void PasteBuildingSetting_Prefix(PlanetFactory __instance, int objectId)
        {
            if (SimulatedWorld.Initialized && !FactoryManager.Instance.IsIncomingRequest.Value)
            {
                LocalPlayer.Instance.SendPacketToLocalStar(new PasteBuildingSettingUpdate(objectId, BuildingParameters.clipboard, GameMain.localPlanet?.id ?? -1));
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlanetFactory.FlattenTerrainReform))]
        public static void FlattenTerrainReform_Prefix(PlanetFactory __instance, Vector3 center, float radius, int reformSize, bool veinBuried, float fade0)
        {
            if (SimulatedWorld.Initialized && !FactoryManager.Instance.IsIncomingRequest.Value)
            {
                LocalPlayer.Instance.SendPacketToLocalStar(new FoundationBuildUpdatePacket(radius, reformSize, veinBuried, fade0));
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlanetFactory.RemoveVegeWithComponents))]
        public static void RemoveVegeWithComponents_Postfix(PlanetFactory __instance, int id)
        {
            if (SimulatedWorld.Initialized && !PlanetManager.IsIncomingRequest)
            {
                LocalPlayer.Instance.SendPacketToLocalStar(new VegeMinedPacket(GameMain.localPlanet?.id ?? -1, id, 0, false));
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlanetFactory.RemoveVeinWithComponents))]
        public static void RemoveVeinWithComponents_Postfix(PlanetFactory __instance, int id)
        {
            if (SimulatedWorld.Initialized && !PlanetManager.IsIncomingRequest)
            {
                if (LocalPlayer.Instance.IsMasterClient)
                {
                    LocalPlayer.Instance.SendPacketToStar(new VegeMinedPacket(__instance.planetId, id, 0, true), __instance.planet.star.id);
                }
                else
                {
                    LocalPlayer.Instance.SendPacketToLocalStar(new VegeMinedPacket(__instance.planetId, id, 0, true));
                }
            }
        }
    }
}
