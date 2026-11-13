namespace RefundPilot.Domain;

public enum IncidentType { Delay, Cancellation }

public enum CaseStatus { New, Sent, Paid, Rejected }

public enum Verdict { Eligible, NotCovered, Unknown }

public enum DelayCause { Unknown, Weather, PersonsOnTrack, RailStrike, Technical, Other }

public enum PayoutPreference { Cash, Voucher }
