using System.Threading;
using System.Threading.Tasks;

namespace parallel_project
{
    public class MatchmakingManager
    {
        //
        // Hosts a match by creating a named-pipe server and waiting for a client to connect.
        //
        // @param token: Cancels the wait/connect.
        // @returns: Connected GameConnection (server side).
        // @notes: Runs on a background task so the UI doesn't freeze.
        //
        public Task<GameConnection> HostAsync(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                GameConnection conn = GameConnection.CreateServer();
                await conn.StartAsync(token);
                return conn;
            }, token);
        }

        //
        // Joins a hosted match by connecting to the host's named-pipe server.
        //
        // @param token: Cancels the connect.
        // @returns: Connected GameConnection (client side).
        // @notes: Runs on a background task so the UI doesn't freeze.
        //
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
