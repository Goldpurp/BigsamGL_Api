namespace Domain.Enums;

public enum IntegrationKind { Email = 0, Sms = 1, WhatsApp = 2, Payment = 3, CarrierTracking = 4, DocumentStorage = 5 }
public enum IntegrationMessageStatus { Pending = 0, Succeeded = 1, Failed = 2 }
