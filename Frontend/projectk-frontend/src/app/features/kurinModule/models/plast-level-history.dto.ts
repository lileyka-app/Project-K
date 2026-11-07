import { PlastLevel } from "./enums/plast-level.enum";

export interface PlastLevelHistoryDto {
    memberKey?: string;
    plastLevelHistoryKey?: string;
    plastLevel: PlastLevel;
    dateAchieved?: Date | string | null;
    /** A пластунка's ступінь rather than a пластун's: its own name and відзнака in the catalogue. */
    isPlastunka?: boolean;
    /** The ступінь in the catalogue, for its name and відзнака; null for the day of joining. Read only. */
    catalogId?: string | null;
}