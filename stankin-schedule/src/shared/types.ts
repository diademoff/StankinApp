export type Group = string;

export interface ApiDate {
  year: number;
  month: number;
  day: number;
}

export interface Lesson {
  subject: string;
  teacher?: string;
  type: string;
  cabinet?: string;
  subgroup?: string;
  sequencePosition: number;
  sequenceLength: number;
  startTime: { hour: number; minute: number };
  duration: { minutes: number };
  date: string; // ISO "YYYY-MM-DD"
}
