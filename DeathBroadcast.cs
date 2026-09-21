using HarmonyLib;

namespace DeathRecap
{
    // Optional multiplayer sharing. The dying client sends its own finished recap to everyone
    // via a routed RPC; other clients running this mod show it as a chat-style message. The
    // recap text can only be built on the dying player's own client (status effects, stamina
    // history and the killing HitData all live there), which is why it's sent as text rather
    // than recomputed by observers. Clients without the mod simply never register the RPC name
    // and ignore it.
    internal static class DeathBroadcast
    {
        private const string RpcName = "DeathRecap_Death";
        private static bool _registered;

        internal static void Send(string recap)
        {
            if (!Plugin.BroadcastToOthers.Value || ZRoutedRpc.instance == null)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            string playerName = player != null ? player.GetPlayerName() : "?";

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcName, playerName, recap);
        }

        private static void Receive(long sender, string playerName, string recap)
        {
            // Everybody includes us - we already showed our own recap locally.
            if (sender == ZNet.GetUID())
            {
                return;
            }

            if (MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft,
                    Loc.Tr("line_other_player_death", playerName, recap.Replace("\n", " - ")));
            }

            Plugin.Log.LogInfo($"[DeathRecap] {playerName}: {recap.Replace("\n", " | ")}");
        }

        // ZRoutedRpc.instance only exists once networking is up, so register when the game starts
        // rather than at plugin load.
        [HarmonyPatch(typeof(Game), "Start")]
        internal static class Game_Start_RegisterRpc
        {
            private static void Postfix()
            {
                if (_registered || ZRoutedRpc.instance == null)
                {
                    return;
                }

                ZRoutedRpc.instance.Register<string, string>(RpcName, Receive);
                _registered = true;
                Plugin.Log.LogInfo("Registered death broadcast RPC.");
            }
        }
    }
}
