import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { DividerModule } from 'primeng/divider';
import { TagModule } from 'primeng/tag';
import { AvatarModule } from 'primeng/avatar';
import { RippleModule } from 'primeng/ripple';

@Component({
  selector: 'app-landing',
  imports: [
    RouterLink,
    ButtonModule,
    CardModule,
    ToolbarModule,
    DividerModule,
    TagModule,
    AvatarModule,
    RippleModule
  ],
  templateUrl: './landing.html',
  styleUrl: './landing.scss'
})
export class Landing {
  protected readonly features = [
    {
      icon: 'pi pi-upload',
      title: 'Import sheets from Excel (or SAP)',
      text: 'Drop an .xlsx into Storage to register hundreds of sheets at once — Sheet No, Customer, Glass Type, dimensions, quantity. Duplicates are detected and reported. The same bulk endpoint lets SAP push orders later, no code change.'
    },
    {
      icon: 'pi pi-compass',
      title: 'Configurable shopfloor pipeline',
      text: 'Define Storage + as many production floors as you need (Cutting, Edging, Tempering, Lamination, Marking, Blackborder, Packing…). Each floor gets its own colour, sequence, and screen.'
    },
    {
      icon: 'pi pi-arrow-right-arrow-left',
      title: 'One-click movement with audit trail',
      text: 'Multi-select sheets, hit "Send to next" — they move, status flips to In-Process, and a full movement record is written. Every move is visible later in the per-sheet history drawer.'
    },
    {
      icon: 'pi pi-objects-column',
      title: 'Batch mode for lamination & friends',
      text: 'Floors that process loads (Lamination, Tempering) can run in batch mode — auto-confirm grouping or manual batch creation. Whole batches move together; dissolve when needed.'
    },
    {
      icon: 'pi pi-check-square',
      title: 'Status workflow that matches the floor',
      text: 'Pending · In Process · On Hold · Completed · Rejected · Delivered. Set per sheet or in bulk. Hold and reject states surface as alerts on the dashboard.'
    },
    {
      icon: 'pi pi-chart-pie',
      title: 'Executive dashboard with live KPIs',
      text: 'Total sheets, on-shopfloor count, completed, delivered. Status donut. Production-floor chip strip. Alerts panel for sheets on hold, ready for dispatch, added today, movements today.'
    },
    {
      icon: 'pi pi-print',
      title: 'Print-ready daily reports',
      text: 'Two professional daily reports — one for production floors, one for storage. A4-formatted, group headers repeat per page, status pills colour-coded for paper.'
    },
    {
      icon: 'pi pi-download',
      title: 'CSV exports on every report',
      text: 'Backend-generated CSVs (UTF-8 BOM, RFC-4180 quoting) that open cleanly in Excel. Mirrors the active filters on the report. No client-side library required.'
    },
    {
      icon: 'pi pi-database',
      title: 'Complete master setup',
      text: 'Plants, Shopfloors, Processes, Customers, Employees, Roles. Each with code, status, contact details, and tenant-isolated unique constraints.'
    },
    {
      icon: 'pi pi-building',
      title: 'Multi-plant ready',
      text: 'Run one company across multiple factory locations. Configure every plant — address, contact, shopfloors, processes, employees — under a single workspace. Filter dashboards and reports per plant.'
    },
    {
      icon: 'pi pi-credit-card',
      title: 'Plans that scale with you',
      text: 'Three INR plans — Annual (₹4L/yr), Biennial (₹7L/2yr, saves ₹1L), and Unlimited (₹20k/mo, cancel any time). All tiers are uncapped on sheets, users, and shopfloors — you choose on commitment, not features. Switch any time from the billing page.'
    },
    {
      icon: 'pi pi-mobile',
      title: 'Built for shopfloor screens',
      text: 'Industrial UI — high-contrast status colours, large click targets, sticky selection bars, skeleton loading. Works on a wall tablet or a supervisor laptop.'
    }
  ];

  protected readonly stats = [
    { value: '6',     label: 'Status states tracked per sheet' },
    { value: '<1s',   label: 'Move latency end-to-end' },
    { value: '100%',  label: 'Audit trail on every movement' }
  ];

  protected readonly floors = [
    { name: 'Storage',     code: 'STORAGE', detail: '12 sheets · entry point',     severity: 'info'    as const, icon: 'pi pi-box' },
    { name: 'Cutting',     code: 'SF1',     detail: '30 in progress',              severity: 'warn'    as const, icon: 'pi pi-cog' },
    { name: 'Edging',      code: 'SF2',     detail: '15 in progress',              severity: 'warn'    as const, icon: 'pi pi-cog' },
    { name: 'Lamination',  code: 'SF3',     detail: '2 batches active',            severity: 'success' as const, icon: 'pi pi-objects-column' },
    { name: 'Blackborder', code: 'SF4',     detail: '22 sheets · ready to ship',   severity: 'success' as const, icon: 'pi pi-check' }
  ];

  protected readonly steps = [
    {
      num: 1,
      title: 'Create your workspace',
      text: 'Sign up with your company name. You get an isolated tenant, your own Admin user, and an active subscription on the Annual plan from day one.'
    },
    {
      num: 2,
      title: 'Configure floors & import sheets',
      text: 'Add your masters (Plants, Customers, Shopfloors with the right sequence and batch mode). Bulk-import opening stock from Excel into Storage.'
    },
    {
      num: 3,
      title: 'Run live production',
      text: 'Operators select sheets and move them down the pipeline. Supervisors watch the dashboard. Owners print the daily report. Done in a day.'
    }
  ];

  protected readonly plans = [
    { code: 'annual',    name: 'Annual',    price: '₹33,333', sub: '₹4,00,000 billed yearly · uncapped usage',                       popular: false },
    { code: 'biennial',  name: 'Biennial',  price: '₹29,167', sub: '₹7,00,000 billed once / 2 years · save ₹1,00,000 vs annual',     popular: true  },
    { code: 'unlimited', name: 'Unlimited', price: '₹20,000', sub: 'Pay-as-you-go monthly · cancel any time · uncapped usage',       popular: false }
  ];
}
