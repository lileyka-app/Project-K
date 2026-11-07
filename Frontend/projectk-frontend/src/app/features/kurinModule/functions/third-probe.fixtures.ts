import { ProbeProjectDto, ThirdProbeDto } from '../models/probes-and-badges/probe-project.dto';

/** Test data for the third-probe tab: a tab and a project with every field filled in. Kept out of specs so importing it registers no tests. */
export function thirdProbeTab(overrides: Partial<ThirdProbeDto> = {}): ThirdProbeDto {
  return {
    isProjectProbe: true,
    canEdit: true,
    canSign: false,
    canUnsign: false,
    file: {
      openedOn: '2026-03-01', openedOnIsDerived: true, mentorZvyazkovyi: null, mentorVporiadnyk: null, mentorReferent: null,
      reflectionOn: null, reflectionWith: null, reflectionConfirmedAtUtc: null, reflectionConfirmedByName: null,
      campKind: null, campName: null, campFrom: null, campTo: null, campCertificate: null, physicalBadgeId: null, resume: '',
      stages: {
        motivationLetterOn: null, selfReflectionOn: null, applicationSubmittedOn: null, openedOn: null, completedOn: null,
        namedOn: null, archivedOn: null, updatedAtUtc: null, updatedByName: null
      },
      storedBytes: 0, updatedAtUtc: null, updatedByName: null
    },
    projects: [],
    badges: [],
    limits: {
      maxMainProjects: 5, maxSpareProjects: 1, maxSharedMainProjects: 2, maxImagesPerProject: 5, maxImageBytes: 1048576,
      maxCertificateBytes: 2097152, maxStoredBytes: 10485760, maxTextLength: 10000, requiredBadges: 4, youthEndsAtAge: 18
    },
    ...overrides
  };
}

export function probeProject(overrides: Partial<ProbeProjectDto> = {}): ProbeProjectDto {
  return {
    projectKey: 'p-1', title: 'Мандрівка Чорногорою', area: 'Nature', reach: ['GroupOrKurin'], isShared: false, sharedWith: null,
    isSpare: false, startsOn: '2026-11-07', endsOn: '2026-11-09', location: 'Чорногора', eventLeads: null,
    coordinatorName: 'Олена Коваль', coordinatorContact: '+380501112233', description: '', goal: '**Мета**', plan: '', results: '',
    approvedAtUtc: null, approvedByName: null, completedAtUtc: null, completedByName: null, coordinatorComment: null,
    updatedAtUtc: '2026-10-30T10:00:00Z', updatedByName: 'Віталій Романюк', images: [],
    ...overrides
  };
}
