namespace FactoryPulse.Application.Monitoring;

public interface IAlarmManagement
{
    Task<AlarmPage> GetPageAsync(AlarmFilter filter, CancellationToken cancellationToken);
}
