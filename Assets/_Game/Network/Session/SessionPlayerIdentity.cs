using System;
using System.Text;
using Fusion;
using Game.Core.Home;

namespace Game.Network.Session
{
    internal static class SessionPlayerIdentity
    {
        private const int NicknameLimit = 32;
        public static string NicknameOf(NetworkRunner runner, PlayerRef player, PlayerProfile profile)
        {
            if (player == runner.LocalPlayer)
            {
                return SanitiseNickname(profile?.Nickname);
            }

            SessionConnectionTokenCodec.Decode(
                runner.GetPlayerConnectionToken(player),
                out _,
                out var presented);
            return SanitiseNickname(presented);
        }

        public static string UserIdOf(NetworkRunner runner, PlayerRef player, PlayerProfile profile)
        {
            if (player == runner.LocalPlayer)
            {
                return profile?.UserId ?? string.Empty;
            }

            SessionConnectionTokenCodec.Decode(
                runner.GetPlayerConnectionToken(player),
                out _,
                out _,
                out var presented);
            return presented ?? string.Empty;
        }

        public static string SanitiseNickname(string presented)
        {
            if (string.IsNullOrWhiteSpace(presented))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(presented.Length);

            foreach (var character in presented)
            {
                if (!char.IsControl(character))
                {
                    builder.Append(character);
                }

                if (builder.Length >= NicknameLimit)
                {
                    break;
                }
            }

            return builder.ToString().Trim();
        }
    }
}
