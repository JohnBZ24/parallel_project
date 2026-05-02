using System.Threading;
using System.Threading.Tasks;

namespace parallel_project
{
    public class MatchmakingManager
    {
        public Task<GameConnection> HostAsync(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                GameConnection conn = GameConnection.CreateServer();
                await conn.StartAsync(token);
                return conn;
            }, token);
        }

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
