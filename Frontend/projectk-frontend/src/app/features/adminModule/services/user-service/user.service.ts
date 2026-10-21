import { inject, Injectable } from "@angular/core";
import { environment } from "../../../../../environments/environment";
import { HttpClient } from "@angular/common/http";
import { SystemUserRole, UserDto } from "../../models/user.dto";
import { tap } from "rxjs";
import { ClientCacheService } from "../../../kurinModule/services/client-cache/client-cache.service";
import { GROUP_CACHE_PREFIX, MEMBER_CACHE_PREFIX } from "../../../kurinModule/services/client-cache/cache-policy";

@Injectable({
    providedIn: 'root'
})
export class UserService {
    private readonly apiUrl = environment.apiUrl;
    private readonly http = inject(HttpClient);
    private readonly cache = inject(ClientCacheService);

    getAllUsers() {
        return this.http.get<UserDto[]>(
            `${this.apiUrl}/user/users`,
            { withCredentials: true }
        );
    }

    changeUserRole(userId: string, newRole: SystemUserRole) {
        return this.http.post<boolean>(
            `${this.apiUrl}/user/${userId}/role`,
            newRole,
            { withCredentials: true, headers: { 'Content-Type': 'application/json' } }
        ).pipe(
            tap(() => this.invalidateRoleSensitiveCache())
        );
    }

    suspendUser(userId: string) {
        return this.http.post<boolean>(
            `${this.apiUrl}/user/${userId}/suspend`,
            null,
            { withCredentials: true }
        );
    }

    restoreUser(userId: string) {
        return this.http.post<boolean>(
            `${this.apiUrl}/user/${userId}/restore`,
            null,
            { withCredentials: true }
        );
    }

    deleteUser(userId: string) {
        return this.http.delete<boolean>(
            `${this.apiUrl}/user/${userId}`,
            { withCredentials: true }
        ).pipe(
            tap(() => this.invalidateRoleSensitiveCache())
        );
    }

    private invalidateRoleSensitiveCache(): void {
        this.cache.invalidateByPrefix(MEMBER_CACHE_PREFIX);
        this.cache.invalidateByPrefix(GROUP_CACHE_PREFIX);
    }
}
