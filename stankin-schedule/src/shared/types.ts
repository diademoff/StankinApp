export type Group = string;

export interface Lesson {
  id: string;
  date: string;        // "2026-03-29"
  startTime: string;   // "08:30"
  endTime: string;     // "10:00"
  durationMinutes: number;
  groupName: string;
  subject: string;
  teacher?: string;
  type: string;        // "Лекция" | "Семинар" | "Лабораторная работа"
  subgroup?: string;
  cabinet?: string;
  sequencePosition: number;
  sequenceLength: number;
}