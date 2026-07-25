namespace Domain.Enums;

public enum ShippingPriceServiceMethod { Air = 0, Sea = 1, Other = 2 }
public enum ShippingPriceKind { Fixed = 0, From = 1, Range = 2, CustomQuote = 3 }
public enum ShippingPriceSource { PublishedWebsite = 0, Staff = 1 }
public enum ShippingPriceState { Active = 0, Inactive = 1 }
