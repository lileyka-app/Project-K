/** A right a kurin sets for itself in «Права й видимість»; the server's `KurinCapability`, by name. */
export type KurinCapability =
  | 'SeeContacts'
  | 'SeeBirthDate'
  | 'SeeBirthday'
  | 'SeeAddress'
  | 'SeeParents'
  | 'SeeCardExtras'
  | 'GiveWarning1'
  | 'GiveWarning2'
  | 'GiveWarning3'
  | 'CancelWarning'
  | 'ConfirmAward1'
  | 'ConfirmAward2'
  | 'ConfirmAward3'
  | 'ConfirmAward4'
  | 'RemoveAward'
  | 'SignProbe'
  | 'UnsignProbe'
  | 'ReviewBadge'
  | 'ChooseProbeEdition'
  | 'AssignKurin'
  | 'AssignGroups'
  | 'AssignMembers'
  | 'AssignProvid'
  | 'ReadJournal';

/** The section of the settings page a capability sits in. */
export type CapabilityGroup = 'MemberData' | 'WarningsAwards' | 'ProbesBadges' | 'Agenda' | 'Journal';

/** A column of the policy: who a capability is given to. */
export type PolicyAudience =
  | 'Zvyazkovyi'
  | 'Vykhovnyk'
  | 'Instruktor'
  | 'Kurinnyy'
  | 'KurinProvid'
  | 'Hurtkovyy'
  | 'GroupProvid'
  | 'Youth';

/** How far a capability reaches for one audience. */
export type CapabilityReach = 'None' | 'OwnGroup' | 'Kurin';

export const CAPABILITY_LABELS: Record<KurinCapability, string> = {
  SeeContacts: 'Пошта й телефон',
  SeeBirthDate: 'Дата народження з роком',
  SeeBirthday: 'День народження',
  SeeAddress: 'Адреса й школа',
  SeeParents: 'Контакти батьків',
  SeeCardExtras: 'Додаткові поля краю',
  GiveWarning1: 'Давати першу пересторогу',
  GiveWarning2: 'Давати другу пересторогу',
  GiveWarning3: 'Давати третю пересторогу',
  CancelWarning: 'Скасовувати чужі перестороги',
  ConfirmAward1: 'Підтверджувати перше відзначення',
  ConfirmAward2: 'Підтверджувати друге відзначення',
  ConfirmAward3: 'Підтверджувати третє відзначення',
  ConfirmAward4: 'Підтверджувати четверте відзначення',
  RemoveAward: 'Видаляти підтверджені відзначення',
  SignProbe: 'Підписувати точки й закривати пробу',
  UnsignProbe: 'Знімати підписи',
  ReviewBadge: 'Підтверджувати вмілості',
  ChooseProbeEdition: 'Обирати пробу гуртку',
  AssignKurin: 'Призначати всьому куреню',
  AssignGroups: 'Призначати гурткам',
  AssignMembers: 'Призначати окремим людям',
  AssignProvid: 'Призначати проводу й КВ',
  ReadJournal: 'Бачити журнал дій'
};

export const CAPABILITY_GROUP_LABELS: Record<CapabilityGroup, string> = {
  MemberData: 'Дані юнаків',
  WarningsAwards: 'Перестороги й відзначення',
  ProbesBadges: 'Проби й вмілості',
  Agenda: 'Події й задачі',
  Journal: 'Журнал дій'
};

export const AUDIENCE_LABELS: Record<PolicyAudience, string> = {
  Zvyazkovyi: 'Звʼязковий',
  Vykhovnyk: 'Впорядники',
  Instruktor: 'Інструктори',
  Kurinnyy: 'Курінний',
  KurinProvid: 'Провід куреня',
  Hurtkovyy: 'Гуртковий',
  GroupProvid: 'Провід гуртка',
  Youth: 'Юнаки'
};

export const REACH_LABELS: Record<CapabilityReach, string> = {
  None: '—',
  OwnGroup: 'Свій гурток',
  Kurin: 'Увесь курінь'
};

/** A label from one of the tables above, or the raw name when this build does not know it. */
export function labelOf<K extends string>(labels: Record<K, string>, key: string): string {
  return (labels as Record<string, string>)[key] ?? key;
}
