import { GroupRepository } from '../ports/GroupRepository';

export class LoadGroupsUseCase {
  constructor(private repo: GroupRepository) {}

  async execute(): Promise<string[]> {
    return this.repo.fetchGroups();
  }
}
