namespace BloodLink.Requests.Domain.Errors;

public static class RequestsDomainErrors
{
    public static class BloodRequest
    {
        public const string InvalidId = "request.invalid_id";
        public const string InvalidHospitalId = "request.invalid_hospital_id";
        public const string InvalidUnitsNeeded = "request.invalid_units_needed";
        public const string InvalidComponent = "request.invalid_component";
        public const string InvalidUrgency = "request.invalid_urgency";
        public const string NeededByInPast = "request.needed_by_in_past";
        public const string InvalidStatusTransition = "request.invalid_status_transition";
        public const string UnitCountMismatch = "request.unit_count_mismatch";
        public const string ReasonRequired = "request.reason_required";
        public const string TerminalState = "request.terminal_state";
        public const string CannotCancelInTransit = "request.cannot_cancel_in_transit";
        public const string CannotCancelTerminal = "request.cannot_cancel_terminal";
        public const string NotFound = "request.not_found";
        public const string Forbidden = "request.forbidden";
    }

    public static class Hospital
    {
        public const string InvalidId = "hospital.invalid_id";
        public const string NameRequired = "hospital.name_required";
        public const string NotFound = "hospital.not_found";
    }

    public static class Compatibility
    {
        public const string Incompatible = "compatibility.incompatible";
        public const string NegativeWaste = "compatibility.negative_waste";
    }

    public static class BloodType
    {
        public const string Invalid = "blood_type.invalid";
    }

    public static class GeoLocation
    {
        public const string InvalidLatitude = "geolocation.invalid_latitude";
        public const string InvalidLongitude = "geolocation.invalid_longitude";
    }
}
