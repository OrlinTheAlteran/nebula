﻿using HarmonyLib;
using NebulaAPI;
using NebulaModel.Logger;
using NebulaModel.Packets.Session;
using NebulaWorld;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NebulaPatcher.Patches.Dynamic
{
    [HarmonyPatch(typeof(UIGalaxySelect))]
    internal class UIGalaxySelect_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(UIGalaxySelect._OnOpen))]
        public static void _OnOpen_Postfix(UIGalaxySelect __instance)
        {
            if(Multiplayer.IsActive && Multiplayer.Session.LocalPlayer.IsClient)
            {
                RectTransform galaxySelectRect = __instance.gameObject.GetComponent<RectTransform>();

                galaxySelectRect.Find("star-count").gameObject.SetActive(false);
                galaxySelectRect.Find("resource-multiplier").gameObject.SetActive(false);
                galaxySelectRect.Find("galaxy-seed").GetComponentInChildren<InputField>().enabled = false;
                galaxySelectRect.Find("random-button").gameObject.SetActive(false);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(UIGalaxySelect.EnterGame))]
        public static bool EnterGame_Prefix(UIGalaxySelect __instance)
        {
            if (Multiplayer.IsInMultiplayerMenu)
            {
                Multiplayer.Session.IsInLobby = false;

                if (((LocalPlayer)Multiplayer.Session.LocalPlayer).IsHost)
                {
                    GameDesc gameDesc = __instance.gameDesc;
                    DSPGame.StartGameSkipPrologue(gameDesc);
                }
                else
                {
                    Multiplayer.Session.Network.SendPacket(new StartGameMessage());
                }
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(UIGalaxySelect.CancelSelect))]
        public static bool CancelSelect_Prefix(UIGalaxySelect __instance)
        {
            if (Multiplayer.IsInMultiplayerMenu && Multiplayer.Session.IsInLobby)
            {
                Log.Info($"Closing listening Socket");
                Multiplayer.ShouldReturnToJoinMenu = false;
                Multiplayer.Session.IsInLobby = false;
                Multiplayer.LeaveGame();
            }

            // cant check anymore if we are in multiplayer or not, so just do this without check. will not do any harm C:
            RectTransform galaxySelectRect = __instance.gameObject.GetComponent<RectTransform>();

            galaxySelectRect.Find("star-count").gameObject.SetActive(true);
            galaxySelectRect.Find("resource-multiplier").gameObject.SetActive(true);
            galaxySelectRect.Find("galaxy-seed").GetComponentInChildren<InputField>().enabled = true;
            galaxySelectRect.Find("random-button").gameObject.SetActive(true);

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(UIGalaxySelect.SetStarmapGalaxy))]
        //[HarmonyPatch(nameof(UIGalaxySelect.OnStarCountSliderValueChange))]
        public static void OnGalaxyStructureChanged_Postfix(UIGalaxySelect __instance)
        {
            if(Multiplayer.IsInMultiplayerMenu && Multiplayer.Session.LocalPlayer.IsHost)
            {
                // syncing players are those who have not loaded into the game yet, so they might still be in the lobby. they need to check if this packet is relevant for them in the corresponding handler.
                // just remembered others cant be in game anyways when host ist still in lobby >.>
                using (Multiplayer.Session.Network.PlayerManager.GetSyncingPlayers(out Dictionary<INebulaConnection, INebulaPlayer> syncingPlayers))
                {
                    foreach(KeyValuePair<INebulaConnection, INebulaPlayer> entry in syncingPlayers)
                    {
                        entry.Key.SendPacket(new LobbyUpdateValues(__instance.gameDesc.galaxyAlgo, __instance.gameDesc.galaxySeed, __instance.gameDesc.starCount, __instance.gameDesc.resourceMultiplier));
                    }
                }
            }
        }
    }
}
