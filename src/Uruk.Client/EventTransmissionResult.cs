using System;

namespace Uruk.Client
{
    public class EventTransmissionResult
    {
        public EventTransmissionStatus Status { get; private set; }

        public EventTransmissionError? ErrorMessage { get; private set; }

        public Exception? Exception { get; private set; }

        public static EventTransmissionResult Success()
        {
            return new EventTransmissionResult { Status = EventTransmissionStatus.Success };
        }

        public static EventTransmissionResult Error()
        {
            return new EventTransmissionResult { Status = EventTransmissionStatus.Error };
        }

        public static EventTransmissionResult Error(Exception exception)
        {
            return new EventTransmissionResult { Status = EventTransmissionStatus.Error, Exception = exception };
        }

        public static EventTransmissionResult Error(string error, string? description = null)
        {
            return new EventTransmissionResult { Status = EventTransmissionStatus.Error, ErrorMessage = new EventTransmissionError(error, description) };
        }

        public static EventTransmissionResult Warning(string error, string? description = null)
        {
            return new EventTransmissionResult { Status = EventTransmissionStatus.Warning, ErrorMessage = new EventTransmissionError(error, description) };
        }
    }
}
