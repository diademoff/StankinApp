export interface GroupRepository {
  fetchGroups(): Promise<string[]>;
}
