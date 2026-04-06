import { GroupRepository } from '../ports/GroupRepository';

export class LoadTeachersUseCase {
  constructor(private repo: GroupRepository) {}

  async execute(): Promise<string[]> {
    return this.repo.fetchTeachers();
  }
}