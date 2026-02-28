// Breath Fan RPM Reader for Unity
// Connect: Yellow (tach) -> D2, Black (GND) -> GND
// Add 10k resistor between D2 and 5V (pull-up)

volatile unsigned long pulseCount = 0;
unsigned long lastTime = 0;
const int TACH_PIN = 2;

void countPulse() {
  pulseCount++;
}

void setup() {
  Serial.begin(9600);
  pinMode(TACH_PIN, INPUT_PULLUP);
  attachInterrupt(digitalPinToInterrupt(TACH_PIN), countPulse, FALLING);
  lastTime = millis();
}

void loop() {
  unsigned long now = millis();
  if (now - lastTime >= 100) { // Update 10x per second
    noInterrupts();
    unsigned long count = pulseCount;
    pulseCount = 0;
    interrupts();
    
    // Most fans: 2 pulses per revolution
    float rpm = (count * 60.0 * 1000.0) / (2.0 * (now - lastTime));
    
    Serial.print("RPM:");
    Serial.println((int)rpm);
    
    lastTime = now;
  }
}