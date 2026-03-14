/// Packing list model for trip preparation.

/// Category of packing items.
enum PackingCategory {
  clothing, toiletries, electronics, documents, medications,
  food, gear, entertainment, accessories, other;

  String get label {
    switch (this) {
      case PackingCategory.clothing: return 'Clothing';
      case PackingCategory.toiletries: return 'Toiletries';
      case PackingCategory.electronics: return 'Electronics';
      case PackingCategory.documents: return 'Documents';
      case PackingCategory.medications: return 'Medications';
      case PackingCategory.food: return 'Food & Snacks';
      case PackingCategory.gear: return 'Gear';
      case PackingCategory.entertainment: return 'Entertainment';
      case PackingCategory.accessories: return 'Accessories';
      case PackingCategory.other: return 'Other';
    }
  }

  String get emoji {
    switch (this) {
      case PackingCategory.clothing: return '👕';
      case PackingCategory.toiletries: return '🧴';
      case PackingCategory.electronics: return '🔌';
      case PackingCategory.documents: return '📄';
      case PackingCategory.medications: return '💊';
      case PackingCategory.food: return '🍎';
      case PackingCategory.gear: return '🎒';
      case PackingCategory.entertainment: return '🎮';
      case PackingCategory.accessories: return '🕶️';
      case PackingCategory.other: return '📦';
    }
  }
}

/// Trip type for template suggestions.
enum TripType {
  beach, business, camping, cityBreak, skiing, roadTrip, backpacking, custom;

  String get label {
    switch (this) {
      case TripType.beach: return 'Beach';
      case TripType.business: return 'Business';
      case TripType.camping: return 'Camping';
      case TripType.cityBreak: return 'City Break';
      case TripType.skiing: return 'Skiing';
      case TripType.roadTrip: return 'Road Trip';
      case TripType.backpacking: return 'Backpacking';
      case TripType.custom: return 'Custom';
    }
  }
}

/// A single item in a packing list.
class PackingItem {
  final String id;
  final String name;
  final PackingCategory category;
  final int quantity;
  final bool isPacked;
  final bool isEssential;
  final String? notes;

  const PackingItem({
    required this.id, required this.name, required this.category,
    this.quantity = 1, this.isPacked = false, this.isEssential = false, this.notes,
  });

  PackingItem copyWith({String? name, PackingCategory? category, int? quantity,
      bool? isPacked, bool? isEssential, String? notes}) =>
    PackingItem(id: id, name: name ?? this.name, category: category ?? this.category,
      quantity: quantity ?? this.quantity, isPacked: isPacked ?? this.isPacked,
      isEssential: isEssential ?? this.isEssential, notes: notes ?? this.notes);

  Map<String, dynamic> toJson() => {
    'id': id, 'name': name, 'category': category.name,
    'quantity': quantity, 'isPacked': isPacked, 'isEssential': isEssential, 'notes': notes,
  };

  factory PackingItem.fromJson(Map<String, dynamic> json) => PackingItem(
    id: json['id'] as String, name: json['name'] as String,
    category: PackingCategory.values.firstWhere((c) => c.name == json['category']),
    quantity: json['quantity'] as int? ?? 1, isPacked: json['isPacked'] as bool? ?? false,
    isEssential: json['isEssential'] as bool? ?? false, notes: json['notes'] as String?,
  );
}

/// A complete packing list for a trip.
class PackingList {
  final String id;
  final String name;
  final TripType tripType;
  final DateTime? departureDate;
  final DateTime? returnDate;
  final String? destination;
  final List<PackingItem> items;
  final DateTime createdAt;

  const PackingList({
    required this.id, required this.name, required this.tripType,
    this.departureDate, this.returnDate, this.destination,
    this.items = const [], required this.createdAt,
  });

  int get totalItems => items.length;
  int get packedItems => items.where((i) => i.isPacked).length;
  int get unpackedItems => totalItems - packedItems;
  int get essentialItems => items.where((i) => i.isEssential).length;
  int get packedEssentials => items.where((i) => i.isEssential && i.isPacked).length;
  int get unpackedEssentials => essentialItems - packedEssentials;
  double get progressPercent => totalItems > 0 ? (packedItems / totalItems * 100) : 0;
  bool get isComplete => totalItems > 0 && packedItems == totalItems;

  int? get daysUntilDeparture =>
    departureDate != null ? departureDate!.difference(DateTime.now()).inDays : null;

  int? get tripDurationDays =>
    (departureDate != null && returnDate != null) ? returnDate!.difference(departureDate!).inDays : null;

  Map<PackingCategory, List<PackingItem>> get itemsByCategory {
    final map = <PackingCategory, List<PackingItem>>{};
    for (final item in items) map.putIfAbsent(item.category, () => []).add(item);
    return map;
  }

  Map<String, dynamic> toJson() => {
    'id': id, 'name': name, 'tripType': tripType.name,
    'departureDate': departureDate?.toIso8601String(),
    'returnDate': returnDate?.toIso8601String(), 'destination': destination,
    'items': items.map((i) => i.toJson()).toList(), 'createdAt': createdAt.toIso8601String(),
  };

  factory PackingList.fromJson(Map<String, dynamic> json) => PackingList(
    id: json['id'] as String, name: json['name'] as String,
    tripType: TripType.values.firstWhere((t) => t.name == json['tripType']),
    departureDate: json['departureDate'] != null ? DateTime.parse(json['departureDate'] as String) : null,
    returnDate: json['returnDate'] != null ? DateTime.parse(json['returnDate'] as String) : null,
    destination: json['destination'] as String?,
    items: (json['items'] as List?)?.map((i) => PackingItem.fromJson(i as Map<String, dynamic>)).toList() ?? [],
    createdAt: DateTime.parse(json['createdAt'] as String),
  );
}
