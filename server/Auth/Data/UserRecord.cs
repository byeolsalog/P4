namespace Auth.Data
{
    public class UserRecord
    {
        public long Id { get; set; }
        public string Provider { get; set; } = default!;
        public string ProviderSub { get; set; } = default!;
        public string? DisplayName { get; set; }
        public string? Email { get; set; }

        // ↓ 서버 코드에서 매핑하려 했던 컬럼들
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }

        // (선택) 밴/상태 관리용
        public byte Status { get; set; }
    }
}