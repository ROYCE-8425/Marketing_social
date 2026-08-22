namespace DXOS.Application;

public sealed class CampaignCopyStub
{
    public string DraftFromTopic(string topic)
    {
        var normalized = string.IsNullOrWhiteSpace(topic) ? "untitled" : topic.Trim();
        return $"Bản nháp DX-OS cho «{normalized}». Chỉ kêu gọi điền form. Chưa phát hành quảng cáo, chưa gọi mô hình ngôn ngữ.";
    }
}
