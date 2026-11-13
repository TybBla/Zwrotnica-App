namespace RefundPilot.Domain;

public enum IncidentType { Delay, Cancellation }

public enum CaseStatus { New, Sent, Paid, Rejected }

public enum Verdict { Eligible, NotCovered, Unknown }

public enum DelayCause { Unknown, Weather, PersonsOnTrack, RailStrike, Technical, Other }

/// <summary>OriginalPayment is the default (value 0): refund to the account or card the ticket was paid with.</summary>
public enum PayoutPreference { OriginalPayment, BankTransfer, Voucher }
