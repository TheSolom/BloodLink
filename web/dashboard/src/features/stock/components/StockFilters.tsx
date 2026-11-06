import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/shared/ui/select";

import { BLOOD_COMPONENTS, BLOOD_TYPES } from "../constants/stock.constants";

export interface StockFiltersProps {
    bloodType: string;
    component: string;
    onBloodTypeChange: (value: string) => void;
    onComponentChange: (value: string) => void;
}

export function StockFilters({
    bloodType,
    component,
    onBloodTypeChange,
    onComponentChange,
}: Readonly<StockFiltersProps>) {
    return (
        <section
            aria-label="Stock Filters"
            className="flex flex-wrap items-center gap-4 bg-neutral-50 p-4 rounded-lg border border-neutral-200"
        >
            <div className="flex items-center gap-2">
                <label htmlFor="blood-type-filter" className="text-sm font-medium text-neutral-700">
                    Blood Type:
                </label>
                <div className="w-36">
                    <Select value={bloodType} onValueChange={onBloodTypeChange}>
                        <SelectTrigger id="blood-type-filter" aria-label="Filter by Blood Type">
                            <SelectValue placeholder="All Blood Types" />
                        </SelectTrigger>
                        <SelectContent>
                            {BLOOD_TYPES.map((type) => (
                                <SelectItem key={type.value} value={type.value}>
                                    {type.label}
                                </SelectItem>
                            ))}
                        </SelectContent>
                    </Select>
                </div>
            </div>

            <div className="flex items-center gap-2">
                <label htmlFor="component-filter" className="text-sm font-medium text-neutral-700">
                    Component:
                </label>
                <div className="w-40">
                    <Select value={component} onValueChange={onComponentChange}>
                        <SelectTrigger id="component-filter" aria-label="Filter by Component">
                            <SelectValue placeholder="All Components" />
                        </SelectTrigger>
                        <SelectContent>
                            {BLOOD_COMPONENTS.map((comp) => (
                                <SelectItem key={comp.value} value={comp.value}>
                                    {comp.label}
                                </SelectItem>
                            ))}
                        </SelectContent>
                    </Select>
                </div>
            </div>
        </section>
    );
}
