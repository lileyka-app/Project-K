import { KurinBranch } from './kurin-branch.enum';
import { branchOfLevel, highestLevel } from './plast-ladder';
import { PlastLevel } from './plast-level.enum';

describe('plast-ladder', () => {
  it('ступінь належить своїй гілці', () => {
    expect(branchOfLevel(PlastLevel.Entry)).toBe(KurinBranch.UPYu);
    expect(branchOfLevel(PlastLevel.Prykhylnyk)).toBe(KurinBranch.UPYu);
    expect(branchOfLevel(PlastLevel.HetmanskiySkob)).toBe(KurinBranch.UPYu);
    expect(branchOfLevel(PlastLevel.Starshoplastun)).toBe(KurinBranch.USP);
    expect(branchOfLevel(PlastLevel.Senior)).toBe(KurinBranch.UPS);
    expect(branchOfLevel(PlastLevel.SeniorPratsi)).toBe(KurinBranch.UPS);
    expect(branchOfLevel(PlastLevel.SeniorDovirja)).toBe(KurinBranch.UPS);
    expect(branchOfLevel(PlastLevel.SeniorKerivnytstva)).toBe(KurinBranch.UPS);
  });

  it('найвищий ступінь — за драбиною, а не за значенням enum', () => {
    expect(highestLevel([PlastLevel.Prykhylnyk, PlastLevel.Uchasnyk])).toBe(PlastLevel.Uchasnyk);
    expect(highestLevel([PlastLevel.Starshoplastun, PlastLevel.Skob])).toBe(PlastLevel.Starshoplastun);
    expect(highestLevel([])).toBeNull();
  });
});
