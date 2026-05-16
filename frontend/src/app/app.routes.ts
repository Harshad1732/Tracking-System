import { Routes } from '@angular/router';
import { Landing } from './landing/landing';
import { Login } from './auth/login/login';
import { Signup } from './auth/signup/signup';
import { ForgotPassword } from './auth/forgot-password/forgot-password';
import { ResetPassword } from './auth/reset-password/reset-password';
import { AppLayout } from './layouts/app-layout/app-layout';
import { Dashboard } from './dashboard/dashboard';
import { PlaceholderPage, PlaceholderConfig } from './shared/placeholder-page/placeholder-page';
import { PlantsPage } from './modules/masters/plants/plants-page';
import { ProcessesPage } from './modules/masters/processes/processes-page';
import { EmployeesPage } from './modules/masters/employees/employees-page';
import { CustomersPage } from './modules/masters/customers/customers-page';
import { RolesPage } from './modules/masters/roles/roles-page';
import { ShopfloorsPage } from './modules/masters/shopfloors/shopfloors-page';
import { ShopfloorPage } from './modules/tracking/shopfloor-page';
import { ShopfloorWorkspacePage } from './modules/tracking/shopfloor-workspace';
import { SheetsReportPage, SheetsReportConfig } from './modules/reports/sheets-report-page';
import { ProcessReportPage } from './modules/reports/process-report-page';
import { DailyReportPage } from './modules/reports/daily-report-page';
import { PlansPage } from './modules/billing/plans-page';
import { BillingPage } from './modules/billing/billing-page';
import { AdminPage } from './modules/admin/admin-page';
import { authGuard } from './auth/auth.guards';

const ph = (placeholder: PlaceholderConfig) => ({ placeholder });
const report = (config: SheetsReportConfig) => ({ report: config });

export const routes: Routes = [
  { path: '', component: Landing, pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'signup', component: Signup },
  { path: 'forgot-password', component: ForgotPassword },
  { path: 'reset-password', component: ResetPassword },
  { path: 'plans', component: PlansPage },

  {
    path: '',
    component: AppLayout,
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', component: Dashboard },

      // Shopfloor tracking
      { path: 'shopfloors',       component: ShopfloorWorkspacePage },
      { path: 'shopfloors/:code', component: ShopfloorPage },

      // Masters
      { path: 'masters/plants', component: PlantsPage },
      { path: 'masters/shopfloors', component: ShopfloorsPage },
      { path: 'masters/processes', component: ProcessesPage },
      { path: 'masters/employees', component: EmployeesPage },
      { path: 'masters/customers', component: CustomersPage },
      { path: 'masters/roles', component: RolesPage },

      // Tracking
      { path: 'tracking/sheet-entry', component: PlaceholderPage, data: ph({
          title: 'Sheet Entry',
          icon: 'pi pi-plus-circle',
          description: 'Register a new glass sheet entering the company.',
          bullets: ['Sheet number / barcode', 'Customer & dimensions', 'Initial process and status']
      })},
      { path: 'tracking/sheet-movement', component: PlaceholderPage, data: ph({
          title: 'Sheet Movement',
          icon: 'pi pi-arrow-right-arrow-left',
          description: 'Move sheets between floors, mark hold, completed or delivered.',
          bullets: ['Select sheet', 'Move to next process', 'Add remarks, capture operator & time']
      })},
      { path: 'tracking/current', component: PlaceholderPage, data: ph({
          title: 'Current Tracking',
          icon: 'pi pi-map-marker',
          description: 'Live grid of every sheet, where it is, and its current status.',
          bullets: ['Filter by plant, process, customer', 'Days-in-process', 'Real-time updates']
      })},

      // Reports
      { path: 'reports/shopfloor', component: SheetsReportPage, data: report({
          title: 'Shopfloor Live Report',
          icon: 'pi pi-th-large',
          crumbLabel: 'Shopfloor Live',
          description: 'All sheets currently inside the company — across every floor.',
          excludeStatuses: ['Delivered']
      })},
      { path: 'reports/delivered', component: SheetsReportPage, data: report({
          title: 'Delivered Report',
          icon: 'pi pi-check-square',
          crumbLabel: 'Delivered',
          description: 'Sheets that have been handed over to the customer.',
          apiStatus: 'Delivered'
      })},
      { path: 'reports/process', component: ProcessReportPage },
      { path: 'reports/daily',   component: DailyReportPage, data: { scope: 'production' } },
      { path: 'reports/storage', component: DailyReportPage, data: { scope: 'storage' } },

      { path: 'billing', component: BillingPage },

      { path: 'administration', component: AdminPage }
    ]
  },

  { path: '**', redirectTo: '' }
];
