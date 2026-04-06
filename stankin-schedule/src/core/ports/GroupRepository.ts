export interface GroupRepository {
  fetchGroups(): Promise<string[]>;
  fetchTeachers(): Promise<string[]>;
}