namespace HeThongDatTiecCuoi_API.Constants;

public static class ScheduleChangeStatusCodes
{
    public const string Pending = "PENDING";
    public const string UnderReview = "UNDER_REVIEW";
    public const string AwaitingQuoteAcceptance = "AWAITING_QUOTE_ACCEPTANCE";
    public const string PaymentPending = "PAYMENT_PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
    public const string ReconciliationRequired = "RECONCILIATION_REQUIRED";

    public static readonly string[] Open =
    [
        Pending, UnderReview, AwaitingQuoteAcceptance, PaymentPending, ReconciliationRequired
    ];
}

public static class ScheduleChangeSources
{
    public const string Customer = "CUSTOMER";
    public const string HallManager = "HALL_MANAGER";
}

public static class ScheduleChangeOtpPurposes
{
    public const string CreateRequest = "SCHEDULE_CHANGE_CREATE";
    public const string AcceptQuote = "SCHEDULE_CHANGE_ACCEPT_QUOTE";
}

public static class ScheduleChangeHoldStatusCodes
{
    public const string Active = "ACTIVE";
    public const string Consumed = "CONSUMED";
    public const string Released = "RELEASED";
    public const string Expired = "EXPIRED";
}

public static class ScheduleChangeQuoteStatusCodes
{
    public const string PendingCustomer = "PENDING_CUSTOMER";
    public const string Accepted = "ACCEPTED";
    public const string Superseded = "SUPERSEDED";
    public const string Cancelled = "CANCELLED";
}

public static class PaymentTransactionTypes
{
    public const string Deposit = "DEPOSIT";
    public const string ScheduleChange = "SCHEDULE_CHANGE";
}

public static class ScheduleChangePolicyStatusCodes
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Retired = "RETIRED";
}

