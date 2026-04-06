import { ScheduleRepository } from '../ports/ScheduleRepository';
import { Lesson } from '../../shared/types';

export class LoadTeacherScheduleWeekUseCase {
  constructor(private repo: ScheduleRepository) {}

  async execute(teacherName: string, startDate: string, endDate: string): Promise<Lesson[]> {
    if (!teacherName) throw new Error('Teacher name is required');
    return this.repo.fetchTeacherWeek(teacherName, startDate, endDate);
  }
}