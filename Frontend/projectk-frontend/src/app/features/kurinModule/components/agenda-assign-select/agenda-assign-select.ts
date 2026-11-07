import { ChangeDetectionStrategy, ChangeDetectorRef, Component, computed, effect, inject, input, model, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TreeSelectModule } from '@openng/optimus-ui/treeselect';
import { TreeNode } from '@openng/optimus-ui/api';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { AgendaAssignTargets, AgendaLeadershipTarget, AgendaMemberTarget, AgendaTagTarget, AgendaTargetInput, AgendaTargetType } from '../../models/agenda';
import { KurinFeaturesService } from '../../services/kurin-features-service/kurin-features.service';

interface TargetNodeData {
  targetType: AgendaTargetType;
  targetKey: string;
}

/**
 * «Призначити для»: дерево Курінь (КВ із його членами) → Гуртки → Учасники → Поза гуртками → Теги, з пошуком (`filter`). Бекенд позначає кожну ціль
 * тим, чи дає її поточному користувачу політика куреня, тож тут немає власної перевірки прав. Вузол,
 * який не можна обрати і під яким нема чого обрати, не малюється зовсім: Гуртковому нема чого
 * дивитись на «весь курінь», якого він не може вибрати.
 */
@Component({
  selector: 'app-agenda-assign-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TreeSelectModule],
  templateUrl: './agenda-assign-select.html',
  styleUrl: './agenda-assign-select.css'
})
export class AgendaAssignSelectComponent implements OnInit {
  protected readonly features = inject(KurinFeaturesService);
  private readonly agendaService = inject(AgendaService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly kurinKey = input.required<string>();
  /** Two-way selection as backend target inputs. */
  readonly targets = model<AgendaTargetInput[]>([]);

  protected readonly nodes = signal<TreeNode<TargetNodeData>[]>([]);
  protected selectedNodes: TreeNode<TargetNodeData>[] = [];

  constructor() {
    // Re-map the selection whenever the tree loads or the bound targets change — e.g. the dialog is reused
    // for another item. Without this the component (which stays mounted) keeps the first item's selection,
    // so a saved assignment looks empty on reopen.
    effect(() => {
      this.nodes();
      this.targets();
      this.syncSelectionFromTargets();
      this.cdr.markForCheck();
    });
  }

  private readonly nodeByKey = computed(() => {
    const map = new Map<string, TreeNode<TargetNodeData>>();
    const walk = (list: TreeNode<TargetNodeData>[]) => {
      for (const node of list) {
        if (node.key) {
          map.set(node.key, node);
        }
        if (node.children) {
          walk(node.children);
        }
      }
    };
    walk(this.nodes());
    return map;
  });

  ngOnInit(): void {
    this.agendaService.getAssignTargets(this.kurinKey()).subscribe(tree => {
      this.nodes.set(this.buildNodes(tree));
    });
  }

  onSelectionChange(selected: TreeNode<TargetNodeData>[]): void {
    const targets = (selected ?? [])
      .map(node => node.data)
      .filter((data): data is TargetNodeData => !!data)
      .map(data => ({ targetType: data.targetType, targetKey: data.targetKey }));
    // A target this tree does not draw (tags off, or beyond what this editor may pick) cannot be
    // unticked here, so it stays: the server keeps it too.
    const drawn = this.nodeByKey();
    const unseen = this.targets().filter(target => !drawn.has(`${target.targetType.toLowerCase()}:${target.targetKey}`));
    this.targets.set([...unseen, ...targets]);
  }

  private buildNodes(tree: AgendaAssignTargets): TreeNode<TargetNodeData>[] {
    const roots: TreeNode<TargetNodeData>[] = [];

    // Курінь-вузол несе КВ і Курінний провід як дочірні тіла; показуємо його, якщо є хоч одна ціль
    // курінного рівня (сам курінь чи котресь тіло).
    if (tree.canTargetKurin || tree.kurinLeaderships.length > 0) {
      roots.push({
        key: `kurin:${tree.kurinKey}`,
        label: tree.kurinLabel,
        icon: 'pi pi-flag',
        selectable: tree.canTargetKurin,
        data: { targetType: 'Kurin', targetKey: tree.kurinKey },
        children: tree.kurinLeaderships.map(office => this.leadershipNode(office))
      });
    }

    for (const group of tree.groups) {
      const children: TreeNode<TargetNodeData>[] = [];
      if (group.leadership) {
        children.push(this.leadershipNode(group.leadership));
      }
      for (const member of group.members) {
        children.push(this.memberNode(member));
      }

      roots.push({
        key: `group:${group.groupKey}`,
        label: group.name,
        icon: 'pi pi-sitemap',
        selectable: group.canTargetGroup,
        data: { targetType: 'Group', targetKey: group.groupKey },
        children
      });
    }

    // Люди поза гуртками — кожен окремо. У старшому курені гуртків немає, і це весь склад.
    const unplaced = tree.unplacedMembers ?? [];
    if (unplaced.length > 0) {
      roots.push({
        key: 'unplaced',
        label: tree.groups.length > 0 ? 'Поза гуртками' : 'Люди куреня',
        icon: 'pi pi-users',
        selectable: false,
        children: unplaced.map(member => this.memberNode(member))
      });
    }

    // Теги — окремою гілкою після гуртків: «усі з тегом» поруч із гуртком, а не всередині нього.
    const tags = tree.tags ?? [];
    if (tags.length > 0) {
      roots.push({
        key: 'tags',
        label: 'Теги',
        icon: 'pi pi-tags',
        selectable: false,
        children: tags.map(tag => this.tagNode(tag))
      });
    }

    return this.prune(roots);
  }

  private tagNode(tag: AgendaTagTarget): TreeNode<TargetNodeData> {
    return {
      key: `tag:${tag.kurinTagKey}`,
      label: `Усі з тегом «${tag.name}»`,
      icon: 'pi pi-tag',
      selectable: true,
      data: { targetType: 'Tag' as AgendaTargetType, targetKey: tag.kurinTagKey }
    };
  }

  /** Keeps a node when it can be picked itself or holds something that can; the rest is noise. */
  private prune(list: TreeNode<TargetNodeData>[]): TreeNode<TargetNodeData>[] {
    const kept: TreeNode<TargetNodeData>[] = [];
    for (const node of list) {
      const children = node.children ? this.prune(node.children) : [];
      if (node.selectable || children.length > 0) {
        kept.push({ ...node, children: children.length > 0 ? children : undefined });
      }
    }
    return kept;
  }

  /** Провід цілим, а під КВ — ще й кожен його член поза гуртками окремо. */
  private leadershipNode(office: AgendaLeadershipTarget): TreeNode<TargetNodeData> {
    const members = (office.members ?? []).map(member => this.memberNode(member));
    return {
      key: `leadership:${office.leadershipKey}`,
      label: office.label,
      icon: 'pi pi-users',
      selectable: office.canTarget,
      data: { targetType: 'Leadership' as AgendaTargetType, targetKey: office.leadershipKey },
      children: members.length > 0 ? members : undefined
    };
  }

  private memberNode(member: AgendaMemberTarget): TreeNode<TargetNodeData> {
    return {
      key: `member:${member.memberKey}`,
      label: member.fullName,
      icon: 'pi pi-user',
      selectable: member.canTarget,
      data: { targetType: 'Member' as AgendaTargetType, targetKey: member.memberKey }
    };
  }

  private syncSelectionFromTargets(): void {
    const map = this.nodeByKey();
    const initial = this.targets();
    const selected: TreeNode<TargetNodeData>[] = [];
    for (const target of initial) {
      const key = `${target.targetType.toLowerCase()}:${target.targetKey}`;
      const node = map.get(key);
      if (node) {
        selected.push(node);
      }
    }
    this.selectedNodes = selected;
  }
}
