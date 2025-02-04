﻿#region

using NebulaAPI.GameState;
using NebulaAPI.Packets;
using NebulaModel.Networking;
using NebulaModel.Packets;
using NebulaModel.Packets.Trash;
using NebulaWorld;
using NebulaWorld.Trash;

#endregion

namespace NebulaNetwork.PacketProcessors.Trash;

[RegisterPacketProcessor]
internal class TrashSystemNewTrashCreatedProcessor : PacketProcessor<TrashSystemNewTrashCreatedPacket>
{
    private readonly IPlayerManager playerManager;

    public TrashSystemNewTrashCreatedProcessor()
    {
        playerManager = Multiplayer.Session.Network.PlayerManager;
    }

    protected override void ProcessPacket(TrashSystemNewTrashCreatedPacket packet, NebulaConnection conn)
    {
        var valid = true;
        if (IsHost)
        {
            var player = playerManager.GetPlayer(conn);
            if (player != null)
            {
                playerManager.SendPacketToOtherPlayers(packet, player);
            }
            else
            {
                valid = false;
            }
        }

        if (!valid)
        {
            return;
        }
        var myId = Multiplayer.Session.World.GenerateTrashOnPlayer(packet);

        //Check if myID is same as the ID from the host
        if (myId != packet.TrashId)
        {
            TrashManager.SwitchTrashWithIds(myId, packet.TrashId);
        }
    }
}
