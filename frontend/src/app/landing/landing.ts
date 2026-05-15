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
      icon: 'pi pi-chart-line',
      title: 'Real-Time Production Monitoring',
      text: 'Live visibility into every line, station and machine on the shopfloor so supervisors can react in seconds, not shifts.'
    },
    {
      icon: 'pi pi-box',
      title: 'Work Order Tracking',
      text: 'Track each work order from release to completion with operator, machine and quantity logs at every step.'
    },
    {
      icon: 'pi pi-clock',
      title: 'Downtime & OEE Analysis',
      text: 'Capture downtime reasons and compute Availability, Performance and Quality to drive continuous improvement.'
    },
    {
      icon: 'pi pi-users',
      title: 'Operator Performance',
      text: 'Measure cycle times, scrap and rework per operator with role-based access for the team.'
    },
    {
      icon: 'pi pi-check-circle',
      title: 'Quality & Scrap Insights',
      text: 'Log rejects against defect codes and spot trends before they impact customer deliveries.'
    },
    {
      icon: 'pi pi-chart-bar',
      title: 'Dashboards & Reports',
      text: 'Configurable dashboards for plant managers and exportable shift reports for review meetings.'
    }
  ];

  protected readonly stats = [
    { value: '24/7', label: 'Live shopfloor data' },
    { value: '<2s', label: 'Update latency' },
    { value: '100%', label: 'Paperless traceability' }
  ];

  protected readonly lines = [
    { name: 'Line A', status: 'Running', detail: 'OEE 87%', severity: 'success' as const, icon: 'pi pi-play' },
    { name: 'Line B', status: 'Changeover', detail: '04:12', severity: 'warn' as const, icon: 'pi pi-sync' },
    { name: 'Line C', status: 'Stopped', detail: 'No material', severity: 'danger' as const, icon: 'pi pi-times' },
    { name: 'Line D', status: 'Running', detail: 'OEE 92%', severity: 'success' as const, icon: 'pi pi-play' }
  ];

  protected readonly steps = [
    { num: 1, title: 'Connect', text: 'Onboard your machines, work centers and operators in minutes.' },
    { num: 2, title: 'Capture', text: 'Log production, downtime and scrap from terminals, tablets or PLC signals.' },
    { num: 3, title: 'Improve', text: 'Review live KPIs and act on bottlenecks before they cost you the shift.' }
  ];

}
