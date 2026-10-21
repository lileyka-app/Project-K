import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { UserService } from '../../services/user-service/user.service';
import { SystemUserRole, UserDto } from '../../models/user.dto';
import { TableModule } from '@openng/optimus-ui/table';
import { IconFieldModule } from '@openng/optimus-ui/iconfield';
import { InputIconModule } from '@openng/optimus-ui/inputicon';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { FormsModule } from '@angular/forms';

import { SelectModule } from "@openng/optimus-ui/select";
import { ButtonModule } from '@openng/optimus-ui/button';
import { MessageService, ConfirmationService } from '@openng/optimus-ui/api';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { TagModule } from '@openng/optimus-ui/tag';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';

@Component({
  selector: 'app-users-list',
  imports: [TableModule, InputTextModule, IconFieldModule, InputIconModule, FormsModule, SelectModule, ButtonModule, ConfirmDialogModule, TagModule, EmptyStateComponent],
  providers: [ConfirmationService],
  templateUrl: './users-list.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './users-list.css'
})
export class UsersListComponent implements OnInit {
  users: UserDto[] = [];
  clonedUsers: Record<string, UserDto> = {};
  expandedRows: Record<string, boolean> = {};
  private readonly userService = inject(UserService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  // System-level roles only. Kurin offices are managed on the Leadership screen.
  // Posted by name, so the backend enum's order is not part of the contract.
  roles: { label: string; value: SystemUserRole }[] = [
    { label: 'Адміністратор', value: 'Admin' },
    { label: 'Учасник', value: 'Member' }
  ];

  ngOnInit() {
    this.loadUsers();
  }

  loadUsers() {
    this.userService.getAllUsers().subscribe({
      next: (users) => {
        this.users = users.sort((a, b) => (a.kurinNumber ?? 999) - (b.kurinNumber ?? 999));

        this.users.forEach(user => {
          if (user.kurinNumber !== null) {
            this.expandedRows[user.kurinNumber.toString()] = false;
          } else {
            this.expandedRows['null'] = false;
          }
        });
      },
      error: () => this.messageService.add({ severity: 'error', summary: 'Помилка', detail: 'Не вдалося завантажити користувачів' })
    });
  }

  getKurinGroupLabel(kurinNumber: number | null): string {
    return kurinNumber === null ? 'Без куреня' : `${kurinNumber} курінь`;
  }

  toggleGroup(kurinNumber: number | null) {
    const key = kurinNumber?.toString() ?? 'null';
    this.expandedRows[key] = !this.expandedRows[key];
  }

  deleteUser(user: UserDto) {
    this.confirmationService.confirm({
      message: `Видалити користувача ${user.firstName} ${user.lastName}? Цю дію неможливо скасувати.`,
      header: 'Підтвердження видалення',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.userService.deleteUser(user.userId).subscribe({
          next: () => {
            this.users = this.users.filter(u => u.userId !== user.userId);
            this.messageService.add({ severity: 'success', summary: 'Готово', detail: 'Користувача видалено' });
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Помилка', detail: err.error?.message || 'Не вдалося видалити користувача' });
          }
        });
      }
    });
  }

  // Suspension is the reversible alternative to deletion: the person, their memberships and
  // everything they earned stay; only the sign-in is refused, and every open session is ended.
  suspendUser(user: UserDto) {
    this.confirmationService.confirm({
      message: `${user.firstName} ${user.lastName} не зможе увійти, а всі відкриті сесії буде завершено. `
        + 'Людина, її членства і все зароблене лишаються. Поновити можна будь-коли.',
      header: 'Призупинити акаунт',
      icon: 'pi pi-pause',
      acceptLabel: 'Призупинити',
      rejectLabel: 'Скасувати',
      acceptButtonProps: { label: 'Призупинити', severity: 'warn' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.userService.suspendUser(user.userId).subscribe({
          next: () => {
            user.isSuspended = true;
            this.messageService.add({ severity: 'success', summary: 'Призупинено', detail: `${user.firstName} ${user.lastName} більше не може увійти.` });
          },
          error: (error: unknown) => this.messageService.add({
            severity: 'error',
            summary: 'Не вдалося призупинити',
            detail: failureDetail(error, (error as { error?: { message?: string } })?.error?.message ?? 'Спробуй ще раз.')
          })
        });
      }
    });
  }

  restoreUser(user: UserDto) {
    this.userService.restoreUser(user.userId).subscribe({
      next: () => {
        user.isSuspended = false;
        this.messageService.add({ severity: 'success', summary: 'Поновлено', detail: `${user.firstName} ${user.lastName} знову може увійти.` });
      },
      error: (error: unknown) => this.messageService.add({
        severity: 'error',
        summary: 'Не вдалося поновити',
        detail: failureDetail(error)
      })
    });
  }

  onRowEditInit(user: UserDto) {
    this.clonedUsers[user.userId] = { ...user };
  }

  /** What the role is called on screen; the value itself is the API's name and stays English. */
  roleLabel(role: string): string {
    return this.roles.find(r => r.value === role)?.label ?? role;
  }

  onRowEditSave(user: UserDto) {
    // The select writes the API value into user.role; the label is for reading only.
    const newRoleValue = this.roles.find(r => r.value === user.role)?.value;
    
    if (newRoleValue === undefined) {
      this.messageService.add({ severity: 'error', summary: 'Помилка', detail: 'Обрано некоректну роль.' });
      this.users[this.users.findIndex(u => u.userId === user.userId)] = this.clonedUsers[user.userId];
      delete this.clonedUsers[user.userId];
      return;
    }

    this.userService.changeUserRole(user.userId, newRoleValue).subscribe({
      next: () => {
        delete this.clonedUsers[user.userId];
        this.messageService.add({ severity: 'success', summary: 'Готово', detail: 'Роль оновлено' });
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Помилка', detail: err.error?.message || 'Не вдалося оновити роль' });
        this.users[this.users.findIndex(u => u.userId === user.userId)] = this.clonedUsers[user.userId];
        delete this.clonedUsers[user.userId];
      }
    });
  }

  onRowEditCancel(user: UserDto, index: number) {
    this.users[index] = this.clonedUsers[user.userId];
    delete this.clonedUsers[user.userId];
  }
}
