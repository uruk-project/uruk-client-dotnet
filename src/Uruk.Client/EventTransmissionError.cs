using System;

namespace Uruk.Client
{
    public class EventTransmissionError
    {
        public EventTransmissionError(string error, string? description = null)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Description = description;
        }

        public string Error { get; }

        public string? Description { get; set; }
    }
}
