export interface Plant {
  id: string;
  number: number;
  code: string;
  name: string;
  address: string | null;
  phone: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface PlantInput {
  code: string;
  name: string;
  address: string | null;
  phone: string | null;
  isActive: boolean;
}

export interface Process {
  id: string;
  number: number;
  plantId: string;
  plantName: string;
  code: string;
  name: string;
  sequenceNo: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface ProcessInput {
  plantId: string;
  code: string;
  name: string;
  sequenceNo: number;
  isActive: boolean;
}

export interface Employee {
  id: string;
  number: number;
  code: string;
  name: string;
  mobile: string | null;
  department: string | null;
  designation: string | null;
  plantId: string | null;
  plantName: string | null;
  processId: string | null;
  processName: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface EmployeeInput {
  code: string;
  name: string;
  mobile: string | null;
  department: string | null;
  designation: string | null;
  plantId: string | null;
  processId: string | null;
  isActive: boolean;
}

export interface Customer {
  id: string;
  number: number;
  code: string;
  name: string;
  contactPerson: string | null;
  mobile: string | null;
  email: string | null;
  address: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CustomerInput {
  code: string;
  name: string;
  contactPerson: string | null;
  mobile: string | null;
  email: string | null;
  address: string | null;
  isActive: boolean;
}

export interface Role {
  id: string;
  number: number;
  name: string;
  description: string | null;
  canView: boolean;
  canAdd: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canViewReports: boolean;
  isActive: boolean;
  createdAtUtc: string;
}

export interface RoleInput {
  name: string;
  description: string | null;
  canView: boolean;
  canAdd: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canViewReports: boolean;
  isActive: boolean;
}

export type BatchMode = 'None' | 'AutoConfirm' | 'Manual';

export interface Shopfloor {
  id: string;
  number: number;
  code: string;
  name: string;
  sequenceNo: number;
  isStorage: boolean;
  batchMode: BatchMode;
  processId: string | null;
  processName: string | null;
  sheetCount: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface ShopfloorInput {
  code: string;
  name: string;
  sequenceNo: number;
  isStorage: boolean;
  batchMode: BatchMode;
  processId: string | null;
  isActive: boolean;
}

export interface GlassSheet {
  id: string;
  number: number;
  sheetNo: string;
  orderNo: string | null;
  customerId: string | null;
  customerName: string | null;
  glassType: string | null;
  thickness: number | null;
  width: number | null;
  height: number | null;
  quantity: number;
  status: string;
  currentShopfloorId: string;
  currentShopfloorCode: string;
  currentShopfloorName: string;
  batchId: string | null;
  batchNo: string | null;
  remarks: string | null;
  entryAtUtc: string;
  lastMovedAtUtc: string;
}

export interface BatchSheetSummary {
  id: string;
  sheetNo: string;
  customerName: string | null;
  status: string;
}

export interface Batch {
  id: string;
  number: number;
  batchNo: string;
  currentShopfloorId: string;
  currentShopfloorCode: string;
  currentShopfloorName: string;
  status: string;
  remarks: string | null;
  sheetCount: number;
  createdAtUtc: string;
  lastMovedAtUtc: string;
  closedAtUtc: string | null;
  sheets: BatchSheetSummary[];
}

export interface BatchCreateInput {
  shopfloorId: string;
  sheetIds: string[];
  remarks: string | null;
}

export interface BatchMoveInput {
  batchIds: string[];
  toShopfloorId: string;
  remarks: string | null;
}

export interface BatchStatusInput {
  batchIds: string[];
  status: string;
  remarks: string | null;
}

export interface SheetCreateInput {
  sheetNo: string;
  orderNo: string | null;
  customerId: string | null;
  glassType: string | null;
  thickness: number | null;
  width: number | null;
  height: number | null;
  quantity: number;
  remarks: string | null;
}

export interface SheetBulkResponse {
  created: number;
  skipped: number;
  skippedSheetNos: string[];
}

export interface SheetMovement {
  id: string;
  glassSheetId: string;
  fromShopfloorId: string | null;
  fromShopfloorName: string | null;
  toShopfloorId: string;
  toShopfloorName: string;
  movedByEmail: string | null;
  remarks: string | null;
  status: string | null;
  movedAtUtc: string;
}

export interface SheetListFilters {
  shopfloorId?: string;
  status?: string;
  customerId?: string;
  isStorage?: boolean;
}
