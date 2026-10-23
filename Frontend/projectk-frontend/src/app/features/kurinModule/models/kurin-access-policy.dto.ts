import { CapabilityGroup, CapabilityReach, KurinCapability, PolicyAudience } from './access-policy-labels.model';

/** What one audience gets for one capability, with the default it would have and whether it may change. */
export interface AccessPolicyCellDto {
  audience: PolicyAudience;
  effective: CapabilityReach;
  default: CapabilityReach;
  locked: boolean;
}

/** One row of «Права й видимість». */
export interface KurinCapabilityDto {
  key: KurinCapability;
  group: CapabilityGroup;
  /** Whether the «Юнаки» column may change on this row: only for the data rows. */
  youthConfigurable: boolean;
  /** The reaches a cell of this row may hold, narrowest first: «увесь курінь» has no «свій гурток». */
  reaches: CapabilityReach[];
  /** One cell per audience, in column order. */
  cells: AccessPolicyCellDto[];
}

/** `GET/PUT kurin/{kurinKey}/access-policy`: the columns and the rows, in the order the page shows them. */
export interface KurinAccessPolicyDto {
  audiences: PolicyAudience[];
  capabilities: KurinCapabilityDto[];
}

/** The whole effective matrix, capability → audience → reach; the server keeps only what differs from the defaults. */
export interface SetKurinAccessPolicyRequest {
  grants: Partial<Record<KurinCapability, Partial<Record<PolicyAudience, CapabilityReach>>>>;
}
