using System.Threading;
using System.Threading.Tasks;

namespace parallel_project
{
    public class MatchmakingManager
    {
        /// <summary>
        /// Hosts a match by creating a named-pipe server and waiting for a client to connect.
        /// </summary>
        /// <param name="token">Cancellation for hosting/waiting operations.</param>
        /// <returns>An established <see cref="GameConnection"/> acting as the server.</returns>
        /// <remarks>
        /// Logic: Wraps the async connect/start sequence in a Task so the UI thread stays responsive.
        /// </remarks>
        public Task<GameConnection> HostAsync(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                GameConnection conn = GameConnection.CreateServer();
                await conn.StartAsync(token);
                return conn;
            }, token);
        }

        /// <summary>
        /// Joins a hosted match by connecting to the host's named-pipe server.
        /// </summary>
        /// <param name="token">Cancellation for joining/connecting operations.</param>
        /// <returns>An established <see cref="GameConnection"/> acting as the client.</returns>
        /// <remarks>
        /// Logic: Wraps the async connect/start sequence in a Task so the UI thread stays responsive.
        /// </remarks>
        public Task<GameConnection> JoinAsync(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                GameConnection conn = GameConnection.CreateClient();
                await conn.StartAsync(token);
                return conn;
            }, token);
        }
    }
}
