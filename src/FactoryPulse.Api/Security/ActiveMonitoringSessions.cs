namespace FactoryPulse.Api.Security;

public sealed class ActiveMonitoringSessions
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, Dictionary<string, Action>> users = new(StringComparer.Ordinal);

    public void Add(string userId, string connectionId, Action abort)
    {
        lock (gate)
        {
            if (!users.TryGetValue(userId, out Dictionary<string, Action>? connections))
            {
                connections = new Dictionary<string, Action>(StringComparer.Ordinal);
                users.Add(userId, connections);
            }

            connections.Add(connectionId, abort);
        }
    }

    public void Remove(string userId, string connectionId)
    {
        lock (gate)
        {
            if (users.TryGetValue(userId, out Dictionary<string, Action>? connections))
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    users.Remove(userId);
                }
            }
        }
    }

    public void Disconnect(string userId)
    {
        Dictionary<string, Action>? connections;
        lock (gate)
        {
            users.Remove(userId, out connections);
        }

        if (connections is not null)
        {
            foreach (Action abort in connections.Values)
            {
                abort();
            }
        }
    }
}
