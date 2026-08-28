export interface TeleportDest {
  floor: string;
  at: string;
  label: string;
}

export interface TeleportTrigger {
  label: string;
  icon: string;
  to: TeleportDest[];
}

export const TELEPORTS: Record<string, TeleportTrigger> = {
  'leave_ksu': {
    label: 'Выйти из КСУ',
    icon: '🚪',
    to: [{ floor: '3', at: 'enter_ksu', label: '3 этаж' }],
  },
  'enter_ksu': {
    label: 'Войти в КСУ',
    icon: '🚪',
    to: [{ floor: 'ksu', at: 'leave_ksu', label: 'Кафедра КСУ' }],
  },
  'leave_3_floor': {
    label: 'Выход с 3 этажа',
    icon: '🚪',
    to: [
      { floor: '4', at: 'leave_4_floor', label: '4 этаж' },
      { floor: '2', at: 'leave_2_floor', label: '2 этаж' },
    ],
  },
};
