using Avalonia.Input;
using Flowery.Controls.Scheduler.Contracts;

namespace Flowery.Controls.Scheduler.Utilities;

/// <summary>Typed Avalonia drag data for dropping an external item onto the scheduler.</summary>
public static class ExternalDropHandler
{
    public static readonly DataFormat<ISchedulerDroppable> Format =
        DataFormat.CreateInProcessFormat<ISchedulerDroppable>("Flowery.Scheduler.Appointment");

    public static DataTransfer CreateDataTransfer(ISchedulerDroppable appointment)
    {
        var item = new DataTransferItem();
        item.Set(Format, appointment);
        var data = new DataTransfer();
        data.Add(item);
        return data;
    }
}
