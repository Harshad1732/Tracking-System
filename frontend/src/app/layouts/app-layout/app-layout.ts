import { Component, HostListener, ViewChild, computed, inject, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { TagModule } from 'primeng/tag';
import { BadgeModule } from 'primeng/badge';
import { TooltipModule } from 'primeng/tooltip';
import { Menu, MenuModule } from 'primeng/menu';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MenuItem } from 'primeng/api';
import { AuthService } from '../../auth/auth.service';
import { I18nService } from '../../shared/i18n/i18n.service';
import { TPipe } from '../../shared/i18n/t.pipe';

interface NavLeaf {
  labelKey: string;
  icon: string;
  link: string;
}

interface NavGroup {
  key: string;
  labelKey: string;
  icon: string;
  link?: never;
  children: NavLeaf[];
}

type NavNode = NavLeaf | NavGroup;

@Component({
  selector: 'app-layout',
  imports: [
    NgClass,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ButtonModule,
    AvatarModule,
    TagModule,
    BadgeModule,
    TooltipModule,
    MenuModule,
    ToastModule,
    ConfirmDialogModule,
    TPipe
  ],
  templateUrl: './app-layout.html',
  styleUrl: './app-layout.scss'
})
export class AppLayout {
  protected readonly auth = inject(AuthService);
  protected readonly i18n = inject(I18nService);
  private readonly router = inject(Router);

  @ViewChild('userMenu') private userMenuRef?: Menu;
  @ViewChild('langMenu') private langMenuRef?: Menu;

  protected readonly collapsed = signal(false);
  protected readonly openGroups = signal<Set<string>>(new Set(['masters', 'reports']));

  protected readonly nav: NavNode[] = [
    { labelKey: 'nav.dashboard', icon: 'pi pi-th-large', link: '/dashboard' },
    { labelKey: 'nav.workspace', icon: 'pi pi-compass',  link: '/shopfloors' },
    {
      key: 'masters',
      labelKey: 'nav.masters',
      icon: 'pi pi-database',
      children: [
        { labelKey: 'nav.masters.plant',     icon: 'pi pi-building', link: '/masters/plants' },
        { labelKey: 'nav.masters.shopfloor', icon: 'pi pi-compass',  link: '/masters/shopfloors' },
        { labelKey: 'nav.masters.process',   icon: 'pi pi-sitemap',  link: '/masters/processes' },
        { labelKey: 'nav.masters.employee',  icon: 'pi pi-user',     link: '/masters/employees' },
        { labelKey: 'nav.masters.customer',  icon: 'pi pi-users',    link: '/masters/customers' },
        { labelKey: 'nav.masters.role',      icon: 'pi pi-id-card',  link: '/masters/roles' }
      ]
    },
    {
      key: 'reports',
      labelKey: 'nav.reports',
      icon: 'pi pi-chart-bar',
      children: [
        { labelKey: 'nav.reports.daily',     icon: 'pi pi-print',        link: '/reports/daily' },
        { labelKey: 'nav.reports.storage',   icon: 'pi pi-box',          link: '/reports/storage' },
        { labelKey: 'nav.reports.shopfloor', icon: 'pi pi-th-large',     link: '/reports/shopfloor' },
        { labelKey: 'nav.reports.delivered', icon: 'pi pi-check-square', link: '/reports/delivered' },
        { labelKey: 'nav.reports.process',   icon: 'pi pi-list',         link: '/reports/process' }
      ]
    },
    { labelKey: 'nav.billing',        icon: 'pi pi-credit-card', link: '/billing' },
    { labelKey: 'nav.administration', icon: 'pi pi-cog',         link: '/administration' }
  ];

  protected readonly userMenuItems = computed<MenuItem[]>(() => [
    {
      label: this.auth.user()?.fullName || this.auth.user()?.email || 'Account',
      items: [
        { label: this.i18n.t('user.profile'),  icon: 'pi pi-user', disabled: true },
        { label: this.i18n.t('user.settings'), icon: 'pi pi-cog',  disabled: true },
        { separator: true },
        { label: this.i18n.t('user.logout'),   icon: 'pi pi-sign-out', command: () => this.logout() }
      ]
    }
  ]);

  protected readonly langMenuItems = computed<MenuItem[]>(() =>
    this.i18n.languages.map(l => ({
      label: `${l.native} (${l.label})`,
      icon: this.i18n.lang() === l.code ? 'pi pi-check' : 'pi pi-globe',
      command: () => this.i18n.setLang(l.code)
    }))
  );

  protected openLangMenu(event: Event): void {
    this.langMenuRef?.toggle(event);
  }

  protected isGroup(node: NavNode): node is NavGroup {
    return (node as NavGroup).children !== undefined;
  }

  protected isOpen(key: string): boolean {
    return this.openGroups().has(key);
  }

  protected toggleGroup(key: string): void {
    const next = new Set(this.openGroups());
    if (next.has(key)) next.delete(key);
    else next.add(key);
    this.openGroups.set(next);
  }

  protected toggleCollapse(): void {
    this.collapsed.update(v => !v);
  }

  protected initials(): string {
    const u = this.auth.user();
    if (!u) return '?';
    const base = u.fullName?.trim() || u.email;
    const parts = base.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
    return base.slice(0, 2).toUpperCase();
  }

  protected openUserMenu(event: Event): void {
    this.userMenuRef?.toggle(event);
  }

  protected logout(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: () => this.router.navigateByUrl('/')
    });
  }

  @HostListener('window:resize')
  protected onResize(): void {
    if (typeof window === 'undefined') return;
    if (window.innerWidth < 960 && !this.collapsed()) {
      this.collapsed.set(true);
    }
  }
}
