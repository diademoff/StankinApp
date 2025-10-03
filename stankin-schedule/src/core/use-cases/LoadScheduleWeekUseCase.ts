import { ScheduleRepository } from '../ports/ScheduleRepository';
import { Lesson } from '../../shared/types';

export class LoadScheduleWeekUseCase {
  constructor(private repo: ScheduleRepository) {}

  async execute(group: string, startDate: string, endDate: string): Promise<Lesson[]> {
    if (!group) throw new Error('Group is required');
    return this.repo.fetchWeek(group, startDate, endDate);
  }
}
