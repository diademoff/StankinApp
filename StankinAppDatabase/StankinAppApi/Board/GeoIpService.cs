using MaxMind.GeoIP2;

namespace StankinAppApi.Board;

public class GeoIpService
{
    private readonly DatabaseReader _reader;
    private readonly bool _available;

    public GeoIpService(string dbPath)
    {
        _available = File.Exists(dbPath);
        if (!_available)
        {
            Console.Error.WriteLine($"[GeoIP] База не найдена: {dbPath}. Проверка по гео пропущена (fail-open).");
            return;
        }
        try
        {
            _reader = new DatabaseReader(dbPath);
        }
        catch (Exception ex)
        {
            _available = false;
            Console.Error.WriteLine($"[GeoIP] Не удалось открыть базу {dbPath}: {ex.Message}. Fail-open.");
        }
    }

    public bool IsRussia(string ip)
    {
        if (!_available)
            return true; // ponytail: fail-open без базы/в dev, капча остаётся барьером
        try
        {
            return string.Equals(_reader.Country(ip).Country.IsoCode, "RU", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GeoIP] Ошибка определения страны для {ip}: {ex.Message}");
            return true; // private/нерезолвится → пропускаем, капча рулит
        }
    }
}
