const dateFormatter = new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "numeric",
    day: "numeric",
});

/**
 * Formats an ISO string, timestamp number, or Date object into a localized date string.
 * Returns an empty string if the date is invalid or falsy.
 */
export function formatDate(input?: string | number | Date | null): string {
    if (!input) return "";

    const date = input instanceof Date ? input : new Date(input);

    if (Number.isNaN(date.getTime())) return "";

    return dateFormatter.format(date);
}
