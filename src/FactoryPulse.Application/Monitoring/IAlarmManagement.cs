namespace FactoryPulse.Application.Monitoring;

public interface IAlarmManagement
{
    Task<AlarmPage> GetPageAsync(AlarmFilter filter, CancellationToken cancellationToken);

    Task<AlarmDetails?> AcknowledgeAsync(long id, CancellationToken cancellationToken);
}
