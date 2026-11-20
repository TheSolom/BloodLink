namespace BloodLink.Inventory.Domain.Errors;

public static class InventoryDomainErrors
{
    public static class BloodBank
    {
        public const string NameRequired = "blood_bank.name_required";
        public const string NameTooLong = "blood_bank.name_too_long";
    }

    public static class BloodUnit
    {
        public const string CollectedInFuture = "unit.collected_in_future";
        public const string AlreadyExpired = "unit.already_expired";
        public const string NotAvailable = "unit.not_available";
        public const string Expired = "unit.expired";
        public const string InvalidReservation = "unit.invalid_reservation";
        public const string CannotExpireTerminal = "unit.cannot_expire_terminal";
        public const string NotYetExpired = "unit.not_yet_expired";
        public const string DiscardReasonRequired = "unit.discard_reason_required";
        public const string NotReserved = "unit.not_reserved";
        public const string NotInTransit = "unit.not_in_transit";
    }

    public static class BloodType
    {
        public const string Invalid = "blood_type.invalid";
    }

    public static class DonationNumber
    {
        public const string Invalid = "donation_number.invalid";
    }

    public static class GeoLocation
    {
        public const string OutOfRange = "geo_location.out_of_range";
    }
}
