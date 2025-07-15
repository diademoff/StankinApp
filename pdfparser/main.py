import camelot
import json
import os
from pathlib import Path

# Сектор - это текст на пересечении времени и дня недели
class Sector:
    text: str
    hspan: bool
    vspan: bool

    # 1 - '8:30 - 10:10'
    # 2 - '10:20 - 12:00'
    # 3 - '12:20 - 14:00'
    # 4 - '14:10 - 15:50'
    # 5 - '16:00 - 17:40'
    # 6 - '18:00 - 19:30'
    # 7 - '19:40 - 21:10'
    # 8 - '21:20 - 22:50'
    # Номер пары
    time: int

    # 1 - 'Понедельник'
    # 2 - 'Вторник'
    # 3 - 'Среда'
    # 4 - 'Четверг'
    # 5 - 'Пятница'
    # 6 - 'Суббота'
    dayOfWeek: int

    def __init__(self, cell_obj, time, dayOfWeek):
        if time not in range(1, 9) or dayOfWeek not in range(1, 7):
            raise Exception("wrong time or day of week (Sector class)")

        self.text = cell_obj.text.replace('\n', ' ').strip()
        self.hspan = cell_obj.hspan
        self.vspan = cell_obj.vspan
        self.time = time
        self.dayOfWeek = dayOfWeek

class SectorEncoder(json.JSONEncoder):
    def default(self, obj):
        if isinstance(obj, Sector):
            return obj.__dict__
        return json.JSONEncoder.default(self, obj)

# Получить список из секторов по таблице pdf
def getSectors(cells) -> []:
    # Cells [строка][столбец]
    sectors = []
    currentRow = 1
    delta = 0
    while currentRow < len(cells):
        cellsInRow = cells[currentRow]
        # 0 столбец это название дня недели
        currentCol = 1
        repeatPrev = False
        if cells[currentRow][0].text == '':
            # вставлять со смещением потому что ячейка разделена горизонтально
            delta += 1

        alreadyRepeatedPrev = False
        while currentCol < len(cellsInRow):
            if repeatPrev:
                currentSector = Sector(cells[currentRow][currentCol - 1], currentCol, currentRow - delta)
                repeatPrev = False
                alreadyRepeatedPrev = True
            else:
                currentSector = Sector(cells[currentRow][currentCol], currentCol, currentRow - delta)

            if currentSector.hspan and not alreadyRepeatedPrev:
                # на следующую итерацию этого цикла вставить этот же предмет
                # потому что он занимает 2 ячейки горизонтально
                repeatPrev = True
            if currentSector.text != '':
                sectors.append(currentSector)
            alreadyRepeatedPrev = False
            currentCol += 1

        currentRow += 1
    return sectors

daysOfWeek: dict = {
    1: "Понедельник",
    2: "Вторник",
    3: "Среда",
    4: "Четверг",
    5: "Пятница",
    6: "Суббота",
}

sectorTime: dict = {
    1: "8:30-10:10",
    2: "10:20-12:00",
    3: "12:20-14:00",
    4: "14:10-15:50",
    5: "16:00-17:40",
    6: "18:00-19:30",
    7: "19:40-21:10",
    8: "21:20-22:50",
}

finalData = {
    "Понедельник": [],
    "Вторник": [],
    "Среда": [],
    "Четверг": [],
    "Пятница": [],
    "Суббота": []
}


def extractFinalData(cells):
    sectors = getSectors(cells)
    for day in range(1, 7):
        dayStr: str = daysOfWeek[day]

        # sorted by time
        todaySectors = sorted([s for s in sectors if s.dayOfWeek == day], key=lambda s: s.time)

        todayTimeTable = {
            "8:30-10:10": [],
            "10:20-12:00": [],
            "12:20-14:00": [],
            "14:10-15:50": [],
            "16:00-17:40": [],
            "18:00-19:30": [],
            "19:40-21:10": [],
            "21:20-22:50": []
        }
        for s in todaySectors:
            timeStr: str = sectorTime[s.time]
            todayTimeTable[timeStr].append(s.text)

        # удалить пустые ключи
        keys_to_remove = [key for key, value in todayTimeTable.items() if value == []]
        for key in keys_to_remove:
            todayTimeTable.pop(key)

        finalData[dayStr] = todayTimeTable
    return finalData

def extractByFilename(filename):
    tables = camelot.read_pdf(filename, line_scale=50)
    # Visual debug:
    # camelot.plot(tables[0], kind='joint').show()
    finalData = extractFinalData(tables[0].cells)

    savefilename = Path(os.path.basename(filename)).stem + '.json'
    with open(savefilename, 'w', encoding='utf-8') as f:
        json.dump(finalData, f, ensure_ascii=False, indent=4)

# https://edu.stankin.ru/course/view.php?id=11557
print("folder path: ")
# folder: str = input()
folder: str = '/home/dmff/repos/StankinApp/pdfparser/pdf'
filenames = os.listdir(folder)

for filename in filenames:
    while True:
        try:
            path = os.path.join(folder, filename)
            break
        except PermissionError:
            sleep(1)
    extractByFilename(path)
    print(filename, 'done')