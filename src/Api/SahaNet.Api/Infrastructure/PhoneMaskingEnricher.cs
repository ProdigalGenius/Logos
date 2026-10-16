using SahaNet.Application.Common;
using Serilog.Core;
using Serilog.Events;

namespace SahaNet.Api.Infrastructure;

/// <summary>KVKK: log olaylarının metin özelliklerindeki telefon numaralarını maskeler.</summary>
internal sealed class PhoneMaskingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            if (value is ScalarValue { Value: string text })
            {
                var masked = PhoneMasking.MaskInText(text);
                if (!ReferenceEquals(masked, text) && masked != text)
                {
                    logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(masked)));
                }
            }
        }
    }
}
