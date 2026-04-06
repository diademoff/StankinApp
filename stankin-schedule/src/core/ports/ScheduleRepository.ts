import { Lesson } from '../../shared/types';

export interface ScheduleRepository {
  fetchWeek(group: string, startDate: string, endDate: string): Promise<Lesson[]>;
  fetchTeacherWeek(teacherName: string, startDate: string, endDate: string): Promise<Lesson[]>;
}