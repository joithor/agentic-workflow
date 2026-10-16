export interface Mail { to: string; subject: string; text: string }

// Sends through the team's SMTP relay; the relay address comes from the environment.
export async function send(mail: Mail): Promise<void> {
  const relay = process.env.SMTP_RELAY ?? "localhost:25";
  await fetch(`http://${relay}/send`, { method: "POST", body: JSON.stringify(mail) });
}
