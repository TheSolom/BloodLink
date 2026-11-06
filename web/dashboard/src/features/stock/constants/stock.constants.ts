export const DEFAULT_PAGE_SIZE = 10;

export const BLOOD_TYPES = [
    { value: "ALL", label: "All Types" },
    { value: "A+", label: "A+" },
    { value: "A-", label: "A-" },
    { value: "B+", label: "B+" },
    { value: "B-", label: "B-" },
    { value: "AB+", label: "AB+" },
    { value: "AB-", label: "AB-" },
    { value: "O+", label: "O+" },
    { value: "O-", label: "O-" },
] as const;

export type BloodTypeFilter = (typeof BLOOD_TYPES)[number]["value"];

export const BLOOD_COMPONENTS = [
    { value: "ALL", label: "All Components" },
    { value: "Plasma", label: "Plasma" },
    { value: "RedCells", label: "Red Cells" },
    { value: "WhiteCells", label: "White Cells" },
    { value: "Platelets", label: "Platelets" },
] as const;

export type ComponentFilter = (typeof BLOOD_COMPONENTS)[number]["value"];
