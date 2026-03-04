namespace Toplanti.DataAccess.Concrete.EntityFramework.Models
{
    public class ZoomStatusTransitionRule
    {
        public int ZoomStatusTransitionRuleId { get; set; }
        public byte FromStatusId { get; set; }
        public byte ToStatusId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public string? Description { get; set; }

        public ZoomStatus? FromStatus { get; set; }
        public ZoomStatus? ToStatus { get; set; }
    }
}
