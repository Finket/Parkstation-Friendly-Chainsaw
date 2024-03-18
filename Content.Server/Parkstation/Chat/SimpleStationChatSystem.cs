﻿using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Parkstation.Speech.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.SimpleStation14.Species.Shadowkin.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using EmpathyChatComponent = Content.Shared.Parkstation.Species.Shadowkin.Components.EmpathyChatComponent;

namespace Content.Server.Parkstation.Chat
{
    /// <summary>
    ///     Extensions for Parkstation's chat stuff
    /// </summary>
    public sealed class SimpleStationChatSystem : EntitySystem
    {
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;

        private IEnumerable<INetChannel> GetShadowkinChatClients()
        {
            return Filter.Empty()
                .AddWhereAttachedEntity(entity => HasComp<EmpathyChatComponent>(entity))
                .Recipients
                .Select(p => p.ConnectedClient);
        }

        private IEnumerable<INetChannel> GetAdminClients()
        {
            return _adminManager.ActiveAdmins
                .Select(p => p.ConnectedClient);
        }

        public void SendEmpathyChat(EntityUid source, string message, bool hideChat)
        {
            if (!HasComp<EmpathyChatComponent>(source)) return;

            var clients = GetShadowkinChatClients();
            var admins = GetAdminClients();
            var localMessage = EntitySystem.Get<ShadowkinAccentSystem>().Accentuate(message);
            var messageWrap = Loc.GetString("chat-manager-send-empathy-chat-wrap-message",
                ("empathyChannelName", Loc.GetString("chat-manager-empathy-channel-name")),
                ("message", message));
            var adminMessageWrap = Loc.GetString("chat-manager-send-empathy-chat-wrap-message-admin",
                ("empathyChannelName", Loc.GetString("chat-manager-empathy-channel-name")),
                ("source", source),
                ("message", message));

            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Empathy chat from {ToPrettyString(source):Player}: {message}");

            _chatSystem.TrySendInGameICMessage(source, localMessage, InGameICChatType.Speak, hideChat);
            _chatManager.ChatMessageToMany(ChatChannel.Empathy, message, messageWrap, source, hideChat, true, clients.ToList(), Color.PaleVioletRed);
            _chatManager.ChatMessageToMany(ChatChannel.Empathy, message, adminMessageWrap, source, hideChat, true, admins, Color.PaleVioletRed);
        }
    }
}
