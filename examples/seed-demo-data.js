/**
 * Seed demo data — 5 partners with synthetic history
 * Usage: node examples/seed-demo-data.js
 */

const API_TOKEN = process.env.API_TOKEN || 'dev-token-change-me-in-prod';
const SYNC_URL = process.env.SYNC_URL || 'http://localhost:3500';

const now = new Date();
const subHours = (h) => new Date(now.getTime() - h * 3600 * 1000).toISOString();
const subDays = (d) => new Date(now.getTime() - d * 86400 * 1000).toISOString();

async function sendMsg(threadId, customer, sender, senderType, content, ts, msgId) {
  const payload = {
    page_name: 'demo_page_001',
    channel: 'zalo',
    ten_khach: customer,
    url: `https://app.pancake.vn/?${threadId}`,
    thread_id: threadId,
    messages: [{
      content,
      sender_type: senderType,
      sender_name: sender,
      timestamp: ts,
      pancake_msg_id: msgId
    }]
  };
  const res = await fetch(`${SYNC_URL}/api/sync`, {
    method: 'POST',
    headers: {
      'X-AIECOS-Token': API_TOKEN,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(payload)
  });
  const data = await res.json();
  console.log(`[SEED] ${customer} (${msgId}): HTTP ${res.status}`, data);
}

async function run() {
  console.log('Seeding 5 partners with conversation history to', SYNC_URL);
  
  // Partner 1: Active
  await sendMsg('pzl_demo_001', 'Shop Alpha', 'Shop Alpha', 'customer', 'Đặt giúp shop 2 cái áo thun M nhé', subHours(2), 'demo_alpha_001');
  await sendMsg('pzl_demo_001', 'Shop Alpha', 'AIECOS Demo', 'agent', 'Đã ghi nhận đơn. Bên em gửi xế chuyến 17h nhé', subHours(1), 'demo_alpha_002');
  
  // Partner 2: Sleeping (4 days ago)
  await sendMsg('pzl_demo_002', 'Shop Beta', 'Shop Beta', 'customer', 'Còn size L màu xanh không em?', subDays(4), 'demo_beta_001');
  
  // Partner 3: At-Risk (14 days ago)
  await sendMsg('pzl_demo_003', 'Shop Gamma', 'Shop Gamma', 'customer', 'Bên em đã gửi hàng lô tuần trước', subDays(14), 'demo_gamma_001');
  
  // Partner 4: Dormant (50 days ago)
  await sendMsg('pzl_demo_004', 'Shop Delta', 'Shop Delta', 'customer', 'Đợt tới mình nhập thêm 100 cái', subDays(50), 'demo_delta_001');
  
  // Partner 5: Churned (120 days ago)
  await sendMsg('pzl_demo_005', 'Shop Epsilon', 'Shop Epsilon', 'customer', 'Cảm ơn shop. Hẹn dịp sau.', subDays(120), 'demo_epsilon_001');
  
  console.log('\n✓ Successfully seeded 5 partners (Active / Sleeping / At-Risk / Dormant / Churned)');
  console.log('  Admin UI: http://localhost:8080');
}

run().catch(err => {
  console.error('[FATAL] Seeding failed:', err);
  process.exit(1);
});
