/** The five areas of the third probe («Скобиний Лет», 2023): one project from each. */
export type ProbeProjectArea = 'OathAndIdea' | 'Community' | 'PlastActivities' | 'Nature' | 'Leadership';

/** Whom a project is useful to, as the template lists it. */
export type ProbeProjectReach = 'GroupOrKurin' | 'Stanytsia' | 'UkrainianCommunity' | 'WiderSociety';

export type ThirdProbeCampKind = 'Kvt' | 'Alternative';

/** A picture of a project's texts: its key goes into the Markdown, its link expires. */
export interface ProbeProjectImageDto {
  imageKey: string;
  url: string;
}

/** One project of the third probe, laid out as the УПЮ template asks. */
export interface ProbeProjectDto {
  projectKey: string;
  title: string;
  area: ProbeProjectArea | null;
  reach: ProbeProjectReach[];
  isShared: boolean;
  sharedWith: string | null;
  /** The sixth, held in reserve and swapped in when a main one falls through. */
  isSpare: boolean;
  startsOn: string | null;
  endsOn: string | null;
  location: string | null;
  eventLeads: string | null;
  coordinatorName: string | null;
  coordinatorContact: string | null;
  /** Markdown, like the goal, plan and results; pictures are written as `project-image:{imageKey}`. */
  description: string;
  goal: string;
  plan: string;
  results: string;
  approvedAtUtc: string | null;
  approvedByName: string | null;
  completedAtUtc: string | null;
  completedByName: string | null;
  coordinatorComment: string | null;
  updatedAtUtc: string;
  updatedByName: string | null;
  images: ProbeProjectImageDto[];
}

export interface ThirdProbeCertificateDto {
  /** A signed link that expires. */
  url: string;
  contentType: string;
  fileName: string | null;
}

/** The stages the referent of the third probe tracks, each the day it happened. */
export interface ThirdProbeStagesDto {
  /** «План здачі проби», asked of a 15-year-old. */
  motivationLetterOn: string | null;
  selfReflectionOn: string | null;
  /** «Зголошення» sent to the край. */
  applicationSubmittedOn: string | null;
  openedOn: string | null;
  completedOn: string | null;
  namedOn: string | null;
  archivedOn: string | null;
  updatedAtUtc: string | null;
  updatedByName: string | null;
}

export interface ThirdProbeFileDto {
  /** The day the probe opened, or else the day the previous probe was closed (`openedOnIsDerived`). */
  openedOn: string | null;
  openedOnIsDerived: boolean;
  mentorZvyazkovyi: string | null;
  mentorVporiadnyk: string | null;
  mentorReferent: string | null;
  reflectionOn: string | null;
  reflectionWith: string | null;
  reflectionConfirmedAtUtc: string | null;
  reflectionConfirmedByName: string | null;
  campKind: ThirdProbeCampKind | null;
  campName: string | null;
  campFrom: string | null;
  campTo: string | null;
  campCertificate: ThirdProbeCertificateDto | null;
  physicalBadgeId: string | null;
  resume: string;
  stages: ThirdProbeStagesDto;
  /** What the person's pictures and посвідка take, against `limits.maxStoredBytes`. */
  storedBytes: number;
  updatedAtUtc: string | null;
  updatedByName: string | null;
}

/** A вмілість the person holds, confirmed. */
export interface ThirdProbeBadgeDto {
  badgeId: string;
  title: string;
  imagePath: string | null;
  specialization: string | null;
  confirmedAtUtc: string | null;
  /** Confirmed on or after the day the third probe opened. */
  duringProbe: boolean;
}

/** The limits the server holds, so the page names the same numbers. */
export interface ProbeProjectLimitsDto {
  maxMainProjects: number;
  maxSpareProjects: number;
  maxSharedMainProjects: number;
  maxImagesPerProject: number;
  maxImageBytes: number;
  maxCertificateBytes: number;
  maxStoredBytes: number;
  maxTextLength: number;
  requiredBadges: number;
  youthEndsAtAge: number;
}

/** `GET member/{key}/probes/{probeId}/third-probe`; every write answers with it too. */
export interface ThirdProbeDto {
  isProjectProbe: boolean;
  /** The person themself, or whoever the kurin lets sign their probe. */
  canEdit: boolean;
  /** Confirms the reflection, a project's start and its completion; sets the stages. */
  canSign: boolean;
  canUnsign: boolean;
  file: ThirdProbeFileDto | null;
  projects: ProbeProjectDto[];
  badges: ThirdProbeBadgeDto[];
  limits: ProbeProjectLimitsDto;
}

export interface ProbeProjectSavedDto {
  projectKey: string;
  tab: ThirdProbeDto;
}

export interface SaveProbeProjectRequestDto {
  title: string;
  area: ProbeProjectArea | null;
  reach: ProbeProjectReach[];
  isShared: boolean;
  sharedWith: string | null;
  startsOn: string | null;
  endsOn: string | null;
  location: string | null;
  eventLeads: string | null;
  coordinatorName: string | null;
  coordinatorContact: string | null;
  description: string;
  goal: string;
  plan: string;
  results: string;
}

export interface SaveThirdProbeFileRequestDto {
  mentorZvyazkovyi: string | null;
  mentorVporiadnyk: string | null;
  mentorReferent: string | null;
  reflectionOn: string | null;
  reflectionWith: string | null;
  campKind: ThirdProbeCampKind | null;
  campName: string | null;
  campFrom: string | null;
  campTo: string | null;
  physicalBadgeId: string | null;
  resume: string;
}

/** `PUT …/third-probe/stages`: whole, a null clears the stage. */
export type SetThirdProbeStagesRequestDto = Omit<ThirdProbeStagesDto, 'updatedAtUtc' | 'updatedByName'>;

/** The scheme of a picture's target in the Markdown. */
export const PROJECT_IMAGE_SCHEME = 'project-image:';
