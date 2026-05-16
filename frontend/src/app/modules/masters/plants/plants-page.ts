import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { SkeletonModule } from 'primeng/skeleton';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ConfirmationService, MessageService } from 'primeng/api';
import { PlantsService } from '../plants.service';
import { Plant } from '../master.types';

@Component({
  selector: 'app-plants-page',
  imports: [
    ReactiveFormsModule, DatePipe,
    ButtonModule, TableModule, DialogModule, InputTextModule, TextareaModule,
    ToggleSwitchModule, TagModule, TooltipModule, SkeletonModule,
    IconFieldModule, InputIconModule
  ],
  templateUrl: './plants-page.html',
  styleUrl: './plants-page.scss'
})
export class PlantsPage implements OnInit {
  protected readonly store = inject(PlantsService);
  private readonly fb = inject(FormBuilder);
  private readonly confirm = inject(ConfirmationService);
  private readonly toast = inject(MessageService);

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<Plant | null>(null);
  protected readonly search = signal('');
  protected readonly skeletonRows = Array.from({ length: 5 });

  protected readonly form: FormGroup = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(20)]],
    name: ['', [Validators.required, Validators.maxLength(120)]],
    address: ['', [Validators.maxLength(250)]],
    phone: ['', [Validators.maxLength(30)]],
    isActive: [true]
  });

  protected readonly filtered = computed<Plant[]>(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.store.items();
    if (!q) return list;
    return list.filter(p =>
      p.code.toLowerCase().includes(q) ||
      p.name.toLowerCase().includes(q) ||
      (p.address ?? '').toLowerCase().includes(q) ||
      (p.phone ?? '').toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    void this.store.list().catch(err => this.toastError(err, 'Could not load plants.'));
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.form.reset({ code: '', name: '', address: '', phone: '', isActive: true });
    this.dialogOpen.set(true);
  }

  protected openEdit(plant: Plant): void {
    this.editing.set(plant);
    this.form.reset({
      code: plant.code,
      name: plant.name,
      address: plant.address ?? '',
      phone: plant.phone ?? '',
      isActive: plant.isActive
    });
    this.dialogOpen.set(true);
  }

  protected closeDialog(): void { this.dialogOpen.set(false); }

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const input = {
      code: v.code, name: v.name,
      address: v.address || null, phone: v.phone || null,
      isActive: v.isActive
    };
    const current = this.editing();
    try {
      if (current) {
        await this.store.update(current.id, input);
        this.toast.add({ severity: 'success', summary: 'Plant updated', detail: `${v.name} has been saved.`, life: 2500 });
      } else {
        await this.store.create(input);
        this.toast.add({ severity: 'success', summary: 'Plant added', detail: `${v.name} has been added.`, life: 2500 });
      }
      this.dialogOpen.set(false);
    } catch (err) {
      this.toastError(err, 'Could not save plant.');
    }
  }

  protected onDelete(plant: Plant): void {
    this.confirm.confirm({
      header: 'Delete plant?',
      message: `“${plant.name}” will be permanently removed. This cannot be undone.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete', rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text p-button-secondary',
      accept: async () => {
        try {
          await this.store.remove(plant.id);
          this.toast.add({ severity: 'success', summary: 'Plant deleted', detail: `${plant.name} has been removed.`, life: 2500 });
        } catch (err) {
          this.toastError(err, 'Could not delete plant.');
        }
      }
    });
  }

  protected clearSearch(): void { this.search.set(''); }

  protected hasError(control: string, error: string): boolean {
    const c = this.form.get(control);
    return !!c && c.touched && c.hasError(error);
  }

  private toastError(err: unknown, fallback: string): void {
    const msg = err instanceof HttpErrorResponse
      ? (err.error?.error ?? err.message ?? fallback)
      : fallback;
    this.toast.add({ severity: 'error', summary: 'Failed', detail: msg, life: 3500 });
  }
}
